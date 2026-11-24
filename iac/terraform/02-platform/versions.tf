terraform {
  required_version = ">= 1.6"

  required_providers {
    helm = {
      source  = "hashicorp/helm"
      version = "~> 3.3"
    }
    kubectl = {
      source  = "alekc/kubectl"
      version = "~> 3.0"
    }
    http = {
      source  = "hashicorp/http"
      version = "~> 3.6"
    }
  }
}

# The cluster layer's state holds the kubeconfig (local backend in this demo).
data "terraform_remote_state" "cluster" {
  backend = "local"
  config = {
    path = "${path.module}/../01-cluster/terraform.tfstate"
  }
}

locals {
  kube         = yamldecode(data.terraform_remote_state.cluster.outputs.kubeconfig)
  kube_host    = local.kube.clusters[0].cluster.server
  kube_ca_cert = base64decode(local.kube.clusters[0].cluster["certificate-authority-data"])
  kube_token   = local.kube.users[0].user.token
}

provider "helm" {
  kubernetes = {
    host                   = local.kube_host
    cluster_ca_certificate = local.kube_ca_cert
    token                  = local.kube_token
  }
}

# Applies plain manifests (the Gateway API CRDs) without needing the CRDs at plan time.
provider "kubectl" {
  host                   = local.kube_host
  cluster_ca_certificate = local.kube_ca_cert
  token                  = local.kube_token
  load_config_file       = false
}
