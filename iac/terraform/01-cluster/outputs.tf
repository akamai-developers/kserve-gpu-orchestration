output "cluster_id" {
  description = "ID of the LKE cluster."
  value       = linode_lke_cluster.ai.id
}

output "kubeconfig" {
  description = "Kubeconfig for the cluster (plain YAML)."
  value       = base64decode(linode_lke_cluster.ai.kubeconfig)
  sensitive   = true
}
