variable "cert_manager_version" {
  description = "cert-manager chart version."
  type        = string
  default     = "v1.21.3"
}

variable "gpu_operator_version" {
  description = "NVIDIA GPU Operator chart version."
  type        = string
  default     = "v26.7.3"
}

variable "kserve_version" {
  description = "KServe chart version (kserve-crd, kserve-resources, kserve-runtime-configs)."
  type        = string
  default     = "v0.21.0"
}

variable "gateway_api_version" {
  description = "Gateway API CRDs (standard channel). Must match what NGINX Gateway Fabric is built against."
  type        = string
  default     = "v1.7.0"
}

variable "nginx_gateway_fabric_version" {
  description = "NGINX Gateway Fabric chart version."
  type        = string
  default     = "2.9.0"
}
