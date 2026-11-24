using BeyondTheCode.Infra;
using Pulumi;

return await Deployment.RunAsync(() =>
{
    var config = new Config();

    // Layer 1: LKE cluster with a CPU pool and a tainted, autoscaled GPU pool.
    var cluster = Cluster.Create(config);

    // Layer 2: cert-manager, NVIDIA GPU Operator and KServe, wired to the new cluster.
    Platform.Install(config, cluster.Kubeconfig);

    return new Dictionary<string, object?>
    {
        ["clusterId"] = cluster.ClusterId,
        ["kubeconfig"] = Output.CreateSecret(cluster.Kubeconfig),
    };
});
