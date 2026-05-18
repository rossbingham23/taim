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
echo "  Durable Dashboard: http://localhost:8085"
echo ""

docker compose up --build "$@"
