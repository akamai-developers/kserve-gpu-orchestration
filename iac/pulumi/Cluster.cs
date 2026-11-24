using System.Text;
using Pulumi;
using Pulumi.Linode;
using Pulumi.Linode.Inputs;
using Config = Pulumi.Config; // Pulumi.Linode has its own Config class

namespace BeyondTheCode.Infra;

public sealed record ClusterOutputs(Output<string> ClusterId, Output<string> Kubeconfig);

public static class Cluster
{
    // Node pools carrying this tag are managed by their own resource, so the
    // cluster resource must not remove them on the next update.
    private const string ExternalPoolTag = "gpu-pool";

    public static ClusterOutputs Create(Config config)
    {
        var cluster = new LkeCluster("ai", new LkeClusterArgs
        {
            Label = "beyond-the-code",
            Region = config.Get("region") ?? "de-fra-2",
            K8sVersion = config.Require("k8sVersion"),
            Tags = { "beyond-the-code" },
            ExternalPoolTags = { ExternalPoolTag },
            Pools =
            {
                // CPU pool: platform components, the .NET app and the embedding model.
                new LkeClusterPoolArgs
                {
                    Type = config.Get("cpuNodeType") ?? "g6-standard-4",
                    Count = config.GetInt32("cpuNodeCount") ?? 3,
                },
            },
        });

        // GPU pool as its own resource: own taint, own autoscaler, resizable
        // without touching the cluster.
        _ = new LkeNodePool("gpu", new LkeNodePoolArgs
        {
            ClusterId = cluster.Id.Apply(int.Parse),
            Type = config.Get("gpuNodeType") ?? "g2-gpu-rtx4000a1-s",
            Tags = { ExternalPoolTag },
            Labels = { ["workload"] = "gpu" },
            Autoscaler = new LkeNodePoolAutoscalerArgs
            {
                Min = config.GetInt32("gpuNodeMin") ?? 1,
                Max = config.GetInt32("gpuNodeMax") ?? 2,
            },
            // Only workloads that tolerate this taint (our LLM) land on the GPU nodes.
            Taints =
            {
                new LkeNodePoolTaintArgs { Key = "nvidia.com/gpu", Value = "true", Effect = "NoSchedule" },
            },
        });

        var kubeconfig = cluster.Kubeconfig.Apply(b64 => Encoding.UTF8.GetString(Convert.FromBase64String(b64)));
        return new ClusterOutputs(cluster.Id, kubeconfig);
    }
}
