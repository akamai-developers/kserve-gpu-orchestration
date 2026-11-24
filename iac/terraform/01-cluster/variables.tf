variable "cluster_label" {
  description = "Label of the LKE cluster."
  type        = string
  default     = "beyond-the-code"
}

variable "region" {
  description = "Akamai Cloud region. Must offer the chosen GPU plan (RTX 4000 Ada: e.g. de-fra-2, fr-par)."
  type        = string
  default     = "de-fra-2"
}

variable "k8s_version" {
  description = "LKE Kubernetes version (see: linode-cli lke versions-list)."
  type        = string
  default = "1.37"
}

variable "cpu_node_type" {
  description = "Plan for the CPU node pool (platform components, the .NET app, the embedding model)."
  type        = string
  default     = "g6-standard-4"
}

variable "cpu_node_count" {
  description = "Number of CPU nodes."
  type        = number
  default     = 2
}

variable "gpu_node_type" {
  description = "Plan for the GPU node pool (see: linode-cli linodes types --text | grep -i gpu)."
  type        = string
  default     = "g2-gpu-rtx4000a1-m"
}

variable "gpu_node_min" {
  description = "Minimum number of GPU nodes (the autoscaler never goes below this)."
  type        = number
  default     = 1
}

variable "gpu_node_max" {
  description = "Maximum number of GPU nodes."
  type        = number
  default     = 2
}
