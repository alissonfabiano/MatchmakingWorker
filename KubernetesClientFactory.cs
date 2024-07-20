using k8s;

public class KubernetesClientFactory
{
    private readonly IConfiguration _configuration;

    public KubernetesClientFactory(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public Kubernetes CreateKubernetesClient()
    {
        var config = KubernetesClientConfiguration.BuildConfigFromConfigFile();
        return new Kubernetes(config);
    }
}
