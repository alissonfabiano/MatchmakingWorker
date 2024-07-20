var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddSingleton<KubernetesClientFactory>();
builder.Services.AddHostedService<QueueListenerService>();

var host = builder.Build();
host.Run();

