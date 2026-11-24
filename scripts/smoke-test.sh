#!/usr/bin/env bash
# Calls both models through their OpenAI-compatible APIs (run scripts/port-forward.sh first).
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
QWEN_URL="${QWEN_URL:-http://localhost:8081}"
EMBED_URL="${EMBED_URL:-http://localhost:8082}"

echo "== Models"
curl -sS "$QWEN_URL/openai/v1/models"
echo

echo "== Chat completion (qwen)"
curl -sS "$QWEN_URL/openai/v1/chat/completions" \
  -H 'content-type: application/json' \
  -d @"$ROOT/demo/prompt.json"
echo

echo "== Embedding (embed)"
curl -sS "$EMBED_URL/openai/v1/embeddings" \
  -H 'content-type: application/json' \
  -d '{"model":"embed","input":["query: GPU scheduling in Kubernetes"]}' \
  | head -c 300
echo " …"
