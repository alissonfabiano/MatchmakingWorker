using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using k8s;
using k8s.Models;

public class QueueListenerService : BackgroundService
{
  private readonly ILogger<QueueListenerService> _logger;
  private readonly IConnection _connection;
  private readonly IModel _channel;
  private readonly Kubernetes _kubernetesClient;
  private const int RetryCount = 3;
  private const int RetryDelayMs = 2000;

  public QueueListenerService(
      IConfiguration configuration,
      ILogger<QueueListenerService> logger,
      KubernetesClientFactory kubernetesClientFactory)
  {
    _logger = logger;
    _kubernetesClient = kubernetesClientFactory.CreateKubernetesClient();

    var factory = new ConnectionFactory
    {
      HostName = configuration["RabbitMQ:Host"],
      Port = int.Parse(configuration["RabbitMQ:Port"]!),
      UserName = configuration["RabbitMQ:UserName"],
      Password = configuration["RabbitMQ:Password"]
    };

    _connection = CreateRabbitMqConnection(factory);
    _channel = _connection.CreateModel();
    _channel.QueueDeclare(
        queue: configuration["RabbitMQ:QueueName"],
        durable: false,
        exclusive: false,
        autoDelete: false,
        arguments: null
    );
  }

  protected override Task ExecuteAsync(CancellationToken stoppingToken)
  {
    var consumer = new EventingBasicConsumer(_channel);
    consumer.Received += async (model, ea) =>
    {
      await OnMessageReceived(model, ea);
    };

    _channel.BasicConsume(
        queue: _channel.QueueDeclare().QueueName,
        autoAck: true,
        consumer: consumer
    );

    return Task.CompletedTask;
  }

  private IConnection CreateRabbitMqConnection(ConnectionFactory factory)
  {
    for (int attempt = 0; attempt < RetryCount; attempt++)
    {
      try
      {
        return factory.CreateConnection();
      }
      catch (Exception ex)
      {
        _logger.LogWarning(ex, $"Attempt {attempt + 1} failed to connect to RabbitMQ. Retrying in {RetryDelayMs} ms...");
        Task.Delay(RetryDelayMs).Wait();
      }
    }
    throw new InvalidOperationException("Failed to connect to RabbitMQ after multiple attempts.");
  }

  private async Task OnMessageReceived(object? model, BasicDeliverEventArgs ea)
  {
    var body = ea.Body.ToArray();
    var message = Encoding.UTF8.GetString(body);
    _logger.LogInformation($"Received message: {message}");

    // Process the message to create a match
    await CreateMatch(message);
  }

  private async Task CreateMatch(string messageBody)
  {
    _logger.LogInformation("Creating a match and starting a server...");

    // aqui deve fazer a logica de veriicar se ja tem 2 player na lista, se tiver seguir o codigo e remover da listagem statica ou banco de dados


    

    int port;
    do
    {
      port = GenerateRandomPort();
    } while (!await IsPortAvailable(port));

    var podName = $"game-server-{Guid.NewGuid()}";

    var pod = new V1Pod
    {
      ApiVersion = "v1",
      Kind = "Pod",
      Metadata = new V1ObjectMeta
      {
        Name = podName,
        Labels = new Dictionary<string, string> { { "app", podName } }
      },
      Spec = new V1PodSpec
      {
        Containers = new List<V1Container>
        {
          new V1Container
          {
            Name = "game-server",
            Image = "afabianoo/game-server:latest",
            Ports = new List<V1ContainerPort>
            {
              new V1ContainerPort { ContainerPort = 80 }
            }
          }
        }
      }
    };

    try
    {
      var createdPod = await _kubernetesClient.CreateNamespacedPodAsync(pod, "default");
      _logger.LogInformation($"Pod {createdPod.Metadata.Name} created successfully.");

      var service = new V1Service
      {
        ApiVersion = "v1",
        Kind = "Service",
        Metadata = new V1ObjectMeta
        {
          Name = $"{podName}-service"
        },
        Spec = new V1ServiceSpec
        {
          Selector = new Dictionary<string, string> { { "app", podName } },
          Ports = new List<V1ServicePort>
          {
            new V1ServicePort
            {
              Port = port,
              TargetPort = 80,
              Protocol = "TCP"
            }
          },
          Type = "NodePort"
        }
      };

      var createdService = await _kubernetesClient.CreateNamespacedServiceAsync(service, "default");
      _logger.LogInformation($"Service {createdService.Metadata.Name} created successfully on port {port}.");
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Failed to create pod or service.");
    }
  }

  private int GenerateRandomPort()
  {
    var random = new Random();
    return random.Next(30000, 32767);
  }

  private async Task<bool> IsPortAvailable(int port)
  {
    var services = await _kubernetesClient.ListNamespacedServiceAsync("default");
    foreach (var service in services.Items)
    {
      if (service.Spec.Ports.Any(p => p.Port == port))
      {
        _logger.LogWarning($"Port {port} is already in use by service {service.Metadata.Name}. Trying another port.");
        return false;
      }
    }
    return true;
  }

  public override void Dispose()
  {
    _channel.Dispose();
    _connection.Dispose();
    base.Dispose();
  }
}
