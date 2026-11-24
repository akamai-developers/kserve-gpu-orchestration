locals {
  # Node pools carrying this tag are managed by their own resource, so the
  # cluster resource must not remove them on the next apply.
  external_pool_tag = "gpu-pool"
}

resource "linode_lke_cluster" "ai" {
  label              = var.cluster_label
  region             = var.region
  k8s_version        = var.k8s_version
  tags               = ["beyond-the-code"]
  external_pool_tags = [local.external_pool_tag]

  # CPU pool: platform components, the .NET app and the embedding model.
  pool {
    type  = var.cpu_node_type
    count = var.cpu_node_count
  }
}

# GPU pool as its own resource: own taint, own autoscaler, resizable without
# touching the cluster.
resource "linode_lke_node_pool" "gpu" {
  cluster_id = linode_lke_cluster.ai.id
  type       = var.gpu_node_type
  tags       = [local.external_pool_tag]
  labels     = { workload = "gpu" }

  autoscaler {
    min = var.gpu_node_min
    max = var.gpu_node_max
  }

  # Only workloads that tolerate this taint (our LLM) land on the GPU nodes.
  taint {
    key    = "nvidia.com/gpu"
    value  = "true"
    effect = "NoSchedule"
  }
}
