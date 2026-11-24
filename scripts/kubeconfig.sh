#!/usr/bin/env bash
# Writes the cluster's kubeconfig to ./kubeconfig.yaml (git-ignored).
# Usage: scripts/kubeconfig.sh terraform|pulumi
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
OUT="$ROOT/kubeconfig.yaml"

case "${1:-}" in
  terraform)
    terraform -chdir="$ROOT/iac/terraform/01-cluster" output -raw kubeconfig > "$OUT"
    ;;
  pulumi)
    (cd "$ROOT/iac/pulumi" && pulumi stack output kubeconfig --show-secrets) > "$OUT"
    ;;
  *)
    echo "usage: $0 terraform|pulumi" >&2
    exit 1
    ;;
esac

chmod 600 "$OUT"
echo "Kubeconfig written. Run:"
echo "  export KUBECONFIG=$OUT"
