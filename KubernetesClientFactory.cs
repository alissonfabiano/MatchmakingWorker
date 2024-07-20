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
        var config = KubernetesClientConfiguration.BuildConfigFromConfigFile("/root/.kube/config");
        return new Kubernetes(config);

        // var serverUrl = _configuration["Kubernetes:ServerUrl"];
        // var token = _configuration["Kubernetes:Token"];

        // var config = new KubernetesClientConfiguration
        // {
        //     Host = serverUrl,
        //     AccessToken = token
        // };

        // return new Kubernetes(config);
    }
}
