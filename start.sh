#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

# Create .env from template if it doesn't exist
if [ ! -f .env ]; then
  cp .env.example .env
  echo "Created .env from .env.example — edit it to add your API keys."
fi

# Require Docker
if ! command -v docker &>/dev/null; then
  echo "Error: Docker is not installed. See https://docs.docker.com/get-docker/"
  exit 1
fi

echo "Starting TAIM..."
echo "  App:               http://localhost:3000"
echo "  API:               http://localhost:5000"
echo "  Ollama:            http://localhost:11434"
echo ""

# Start all services (detached so we can wait for Ollama before pulling)
docker compose up --build -d "$@"

# Pull the configured Ollama model if not already present
OLLAMA_MODEL="${OLLAMA_MODEL:-qwen2.5:3b}"
echo "Waiting for Ollama to be ready..."
until docker compose exec -T taim-ollama curl -sf http://localhost:11434/api/tags >/dev/null 2>&1; do
  sleep 2
done

if docker compose exec -T taim-ollama ollama list 2>/dev/null | grep -q "${OLLAMA_MODEL%%:*}"; then
  echo "Ollama model '${OLLAMA_MODEL}' already present — skipping pull."
else
  echo "Pulling Ollama model '${OLLAMA_MODEL}' (first run — this may take a few minutes)..."
  docker compose exec -T taim-ollama ollama pull "${OLLAMA_MODEL}"
  echo "Model ready."
fi

echo ""
echo "Stack is up. Tailing logs (Ctrl-C to stop — services keep running)."
docker compose logs -f taim-api taim-web
