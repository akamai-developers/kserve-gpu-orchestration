# --- Gateway API ---------------------------------------------------------------
# CRDs first: cert-manager checks for them at startup, NGINX Gateway Fabric needs them.
data "http" "gateway_api_crds" {
  url = "https://github.com/kubernetes-sigs/gateway-api/releases/download/${var.gateway_api_version}/standard-install.yaml"
}

data "kubectl_file_documents" "gateway_api_crds" {
  content = data.http.gateway_api_crds.response_body
}

resource "kubectl_manifest" "gateway_api_crds" {
  for_each          = data.kubectl_file_documents.gateway_api_crds.manifests
  yaml_body         = each.value
  server_side_apply = true # the CRDs are too large for client-side apply
}

# NGINX Gateway Fabric (F5's open source Gateway API implementation). It
# provisions an NGINX data plane and a LoadBalancer Service (an Akamai
# NodeBalancer) for every Gateway; see manifests/gateway/.
resource "helm_release" "nginx_gateway_fabric" {
  name             = "ngf"
  chart            = "oci://ghcr.io/nginx/charts/nginx-gateway-fabric"
  version          = var.nginx_gateway_fabric_version
  namespace        = "nginx-gateway"
  create_namespace = true
  set              = [{ name = "nginxGateway.productTelemetry.enable", value = "false" }]
  depends_on       = [kubectl_manifest.gateway_api_crds]
}

# --- cert-manager --------------------------------------------------------------
# Needed by KServe's webhooks. Gateway API support is enabled up front, so the
# HTTPS add-on (docs/https-with-custom-domain.md) needs no platform change.
resource "helm_release" "cert_manager" {
  name             = "cert-manager"
  chart            = "oci://quay.io/jetstack/charts/cert-manager"
  version          = var.cert_manager_version
  namespace        = "cert-manager"
  create_namespace = true
  set = [
    { name = "crds.enabled", value = "true" },
    { name = "config.gatewayAPI.enabled", value = "true" },
  ]
  depends_on = [kubectl_manifest.gateway_api_crds]
}

# --- NVIDIA GPU Operator -------------------------------------------------------
# LKE GPU nodes ship with drivers, so driver and toolkit stay disabled
# (Akamai's recommendation for non-Enterprise LKE clusters).
resource "helm_release" "gpu_operator" {
  name             = "gpu-operator"
  repository       = "https://helm.ngc.nvidia.com/nvidia"
  chart            = "gpu-operator"
  version          = var.gpu_operator_version
  namespace        = "gpu-operator"
  create_namespace = true
  set = [
    { name = "driver.enabled", value = "false" },
    { name = "toolkit.enabled", value = "false" },
  ]
}

# --- KServe --------------------------------------------------------------------
resource "helm_release" "kserve_crd" {
  name             = "kserve-crd"
  chart            = "oci://ghcr.io/kserve/charts/kserve-crd"
  version          = var.kserve_version
  namespace        = "kserve"
  create_namespace = true
}

# KServe controller in Standard mode (no Knative/Istio). Ingress creation is
# disabled: the models stay cluster-internal, only the .NET app is exposed.
resource "helm_release" "kserve" {
  name      = "kserve"
  chart     = "oci://ghcr.io/kserve/charts/kserve-resources"
  version   = var.kserve_version
  namespace = "kserve"
  set = [
    { name = "kserve.controller.deploymentMode", value = "Standard" },
    { name = "kserve.controller.gateway.disableIngressCreation", value = "true" },
    # The storage-initializer downloads model weights; its 1Gi default is
    # OOMKilled by multi-GB models such as Qwen3-4B.
    { name = "kserve.storage.resources.limits.memory", value = "8Gi" },
  ]
  depends_on = [helm_release.cert_manager, helm_release.kserve_crd]
}

# ClusterServingRuntimes, including the Hugging Face runtime (vLLM inside).
resource "helm_release" "kserve_runtimes" {
  name       = "kserve-runtime-configs"
  chart      = "oci://ghcr.io/kserve/charts/kserve-runtime-configs"
  version    = var.kserve_version
  namespace  = "kserve"
  set        = [{ name = "kserve.servingruntime.enabled", value = "true" }]
  depends_on = [helm_release.kserve]
}
