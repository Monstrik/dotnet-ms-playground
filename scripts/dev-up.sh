#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
repo_root="$(cd -- "$script_dir/.." && pwd)"

cd "$repo_root"

echo "Starting microservices stack from $repo_root"
docker compose up -d --build

wait_for_url() {
  local name="$1"
  local url="$2"
  local attempts=30

  for ((i=1; i<=attempts; i++)); do
    if curl --silent --fail "$url" >/dev/null 2>&1; then
      echo "$name is ready: $url"
      return 0
    fi

    sleep 1
  done

  echo "Timed out waiting for $name at $url" >&2
  docker compose ps >&2
  exit 1
}

wait_for_url "monitor" "http://localhost:8080/health"
wait_for_url "plain-js-client" "http://localhost:8086/health"
wait_for_url "echo-service" "http://localhost:8082/health"
wait_for_url "weather-service" "http://localhost:8084/health"

echo
echo "Stack is ready:"
echo "  Monitor:         http://localhost:8080"
echo "  Plain JS Client: http://localhost:8086"
echo "  Echo API:        http://localhost:8082"
echo "  Weather API:     http://localhost:8084"
echo
echo "Useful checks:"
echo "  curl http://localhost:8082/echo/hello"
echo "  curl http://localhost:8084/weather/London"

