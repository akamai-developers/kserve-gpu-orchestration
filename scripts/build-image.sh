#!/usr/bin/env bash
# Builds the app's container image with the .NET SDK (no Dockerfile) and pushes it.
# Log in to your registry first, e.g.:
#   echo "$GITHUB_PAT" | docker login ghcr.io -u <github-user> --password-stdin
# Usage: REGISTRY=ghcr.io/<github-user> scripts/build-image.sh [tag]
set -euo pipefail

TAG="${1:-1.0.0}"
: "${REGISTRY:?Set REGISTRY, e.g. REGISTRY=ghcr.io/<github-user>}"

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
# Image names must be lowercase (GHCR and the .NET SDK both reject uppercase).
REGISTRY="$(printf '%s' "$REGISTRY" | tr '[:upper:]' '[:lower:]')"
HOST="${REGISTRY%%/*}"
NAMESPACE="${REGISTRY#*/}"
IMAGE="$HOST/$NAMESPACE/beyond-the-code-api:$TAG"

# LKE nodes are x64, even when you build on an arm64 Mac.
dotnet publish "$ROOT/src/Api" -c Release --os linux --arch x64 -t:PublishContainer \
  -p:ContainerRegistry="$HOST" \
  -p:ContainerRepository="$NAMESPACE/beyond-the-code-api" \
  -p:ContainerImageTag="$TAG"

echo
echo "Pushed $IMAGE"
echo "Deploy it with:"
echo "  kubectl set image deployment/api api=$IMAGE -n app"
