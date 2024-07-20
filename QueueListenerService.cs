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

  public QueueListenerService(IConfiguration configuration, ILogger<QueueListenerService> logger, KubernetesClientFactory kubernetesClientFactory)
  {
    _logger = logger;

    var factory = new ConnectionFactory
    {
      HostName = configuration["RabbitMQ:Host"],
      Port = int.Parse(configuration["RabbitMQ:Port"]!),
      UserName = configuration["RabbitMQ:UserName"],
      Password = configuration["RabbitMQ:Password"]
    };

    _connection = factory.CreateConnection();
    _channel = _connection.CreateModel();
    _channel.QueueDeclare(queue: configuration["RabbitMQ:QueueName"],
                         durable: false,
                         exclusive: false,
                         autoDelete: false,
                         arguments: null);

    _kubernetesClient = kubernetesClientFactory.CreateKubernetesClient();
  }

  protected override Task ExecuteAsync(CancellationToken stoppingToken)
  {
    var consumer = new EventingBasicConsumer(_channel);
    consumer.Received += async (model, ea) =>
    {
      var body = ea.Body.ToArray();
      var message = Encoding.UTF8.GetString(body);
      _logger.LogInformation($"Received message: {message}");

      // Parse player level and create a room
      await CreateMatchAsync(message);
    };

    _channel.BasicConsume(queue: _channel.QueueDeclare().QueueName,
                          autoAck: true,
                          consumer: consumer);

    return Task.CompletedTask;
  }

  private async Task CreateMatchAsync(string messageBody)
  {
    _logger.LogInformation("Creating a match and starting a server...");

    var pod = new V1Pod
    {
      ApiVersion = "v1",
      Kind = "Pod",
      Metadata = new V1ObjectMeta
      {
        Name = "game-server-" + Guid.NewGuid().ToString()
      },
      Spec = new V1PodSpec
      {
        Containers = new List<V1Container>
                {
                    new V1Container
                    {
                        Name = "game-server",
                        Image = "alissonfabiano/game-server:latest",
                        Ports = new List<V1ContainerPort>
                        {
                            new V1ContainerPort { ContainerPort = 80 }
                        }
                    }
                }
      }
    };
    await Task.Run(() => _kubernetesClient.CreateNamespacedPod(pod, "default"));
  }

  public override void Dispose()
  {
    _channel.Dispose();
    _connection.Dispose();
    base.Dispose();
  }
}
