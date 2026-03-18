#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
repo_root="$(cd -- "$script_dir/.." && pwd)"

cd "$repo_root"

echo "Stopping microservices stack from $repo_root"
docker compose stop -t 30 monitor echo-service weather-service >/dev/null 2>&1 || true
docker compose down --remove-orphans

echo "Stack stopped."

