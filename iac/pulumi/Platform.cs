using Pulumi;
using Pulumi.Kubernetes.Helm.V3;
using Pulumi.Kubernetes.Types.Inputs.Helm.V3;
using K8s = Pulumi.Kubernetes;

namespace BeyondTheCode.Infra;

public static class Platform
{
    public static void Install(Config config, Output<string> kubeconfig)
    {
        var provider = new K8s.Provider("lke", new K8s.ProviderArgs { KubeConfig = kubeconfig });
        var kserveVersion = config.Get("kserveVersion") ?? "v0.21.0";
        var gatewayApiVersion = config.Get("gatewayApiVersion") ?? "v1.7.0";

        // --- Gateway API -------------------------------------------------------
        // CRDs first: cert-manager checks for them at startup, NGINX Gateway Fabric needs them.
        var gatewayApiCrds = new K8s.Yaml.V2.ConfigFile("gateway-api-crds",
            new K8s.Types.Inputs.Yaml.V2.ConfigFileArgs
            {
                File = $"https://github.com/kubernetes-sigs/gateway-api/releases/download/{gatewayApiVersion}/standard-install.yaml",
            },
            new ComponentResourceOptions { Provider = provider });

        // NGINX Gateway Fabric (F5's open source Gateway API implementation). It
        // provisions an NGINX data plane and a LoadBalancer Service (an Akamai
        // NodeBalancer) for every Gateway; see manifests/gateway/.
        HelmRelease(provider, "ngf", "oci://ghcr.io/nginx/charts/nginx-gateway-fabric",
            config.Get("nginxGatewayFabricVersion") ?? "2.9.0", "nginx-gateway",
            values: new() { ["nginxGateway"] = Map(("productTelemetry", Map(("enable", false)))) },
            dependsOn: [gatewayApiCrds]);

        // --- cert-manager ------------------------------------------------------
        // Needed by KServe's webhooks. Gateway API support is enabled up front, so
        // the HTTPS add-on (docs/https-with-custom-domain.md) needs no platform change.
        var certManager = HelmRelease(provider, "cert-manager", "oci://quay.io/jetstack/charts/cert-manager",
            config.Get("certManagerVersion") ?? "v1.21.3", "cert-manager",
            values: new()
            {
                ["crds"] = Map(("enabled", true)),
                ["config"] = Map(("gatewayAPI", Map(("enabled", true)))),
            },
            dependsOn: [gatewayApiCrds]);

        // --- NVIDIA GPU Operator -----------------------------------------------
        // LKE GPU nodes ship with drivers, so driver and toolkit stay disabled
        // (Akamai's recommendation for non-Enterprise LKE clusters).
        HelmRelease(provider, "gpu-operator", "gpu-operator",
            config.Get("gpuOperatorVersion") ?? "v26.7.3", "gpu-operator",
            repository: "https://helm.ngc.nvidia.com/nvidia",
            values: new()
            {
                ["driver"] = Map(("enabled", false)),
                ["toolkit"] = Map(("enabled", false)),
            });

        // --- KServe ------------------------------------------------------------
        var kserveCrd = HelmRelease(provider, "kserve-crd", "oci://ghcr.io/kserve/charts/kserve-crd",
            kserveVersion, "kserve");

        // KServe controller in Standard mode (no Knative/Istio). Ingress creation
        // is disabled: the models stay cluster-internal, only the app is exposed.
        var kserve = HelmRelease(provider, "kserve", "oci://ghcr.io/kserve/charts/kserve-resources",
            kserveVersion, "kserve",
            values: new()
            {
                ["kserve"] = Map(
                    ("controller", Map(
                        ("deploymentMode", "Standard"),
                        ("gateway", Map(("disableIngressCreation", true))))),
                    // The storage-initializer downloads model weights; its 1Gi default
                    // is OOMKilled by multi-GB models such as Qwen3-4B.
                    ("storage", Map(("resources", Map(("limits", Map(("memory", "8Gi"))))))))
            },
            dependsOn: [certManager, kserveCrd]);

        // ClusterServingRuntimes, including the Hugging Face runtime (vLLM inside).
        HelmRelease(provider, "kserve-runtime-configs", "oci://ghcr.io/kserve/charts/kserve-runtime-configs",
            kserveVersion, "kserve",
            values: new() { ["kserve"] = Map(("servingruntime", Map(("enabled", true)))) },
            dependsOn: [kserve]);
    }

    private static Release HelmRelease(
        K8s.Provider provider,
        string name,
        string chart,
        string version,
        string ns,
        string? repository = null,
        InputMap<object>? values = null,
        Resource[]? dependsOn = null)
    {
        var args = new ReleaseArgs
        {
            Name = name,
            Chart = chart,
            Version = version,
            Namespace = ns,
            CreateNamespace = true,
            Values = values ?? new InputMap<object>(),
        };

        if (repository is not null)
        {
            args.RepositoryOpts = new RepositoryOptsArgs { Repo = repository };
        }

        return new Release(name, args, new CustomResourceOptions
        {
            Provider = provider,
            DependsOn = dependsOn ?? Array.Empty<Resource>(),
        });
    }

    private static Dictionary<string, object> Map(params (string Key, object Value)[] entries) =>
        entries.ToDictionary(e => e.Key, e => e.Value);
}
