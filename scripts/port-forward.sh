#!/usr/bin/env bash
# Forwards the cluster-internal model endpoints to localhost:
#   qwen  → http://localhost:8081/openai/v1
#   embed → http://localhost:8082/openai/v1
set -euo pipefail

kubectl port-forward -n models svc/qwen-predictor 8081:80 &
kubectl port-forward -n models svc/embed-predictor 8082:80 &

trap 'kill $(jobs -p) 2>/dev/null' EXIT INT TERM
echo "Forwarding qwen → :8081 and embed → :8082 (Ctrl+C to stop)"
wait
