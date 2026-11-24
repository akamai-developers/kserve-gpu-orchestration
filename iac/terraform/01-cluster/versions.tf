terraform {
  required_version = ">= 1.6"

  required_providers {
    linode = {
      source  = "linode/linode"
      version = "~> 4.10"
    }
  }
}

# Reads the API token from the LINODE_TOKEN environment variable.
provider "linode" {}
