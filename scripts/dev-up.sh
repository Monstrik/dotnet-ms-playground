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

wait_for_rabbitmq() {
  local attempts=30

  for ((i=1; i<=attempts; i++)); do
    if docker compose exec -T rabbitmq rabbitmq-diagnostics -q ping >/dev/null 2>&1; then
      echo "rabbitmq is ready: amqp://app:app@localhost:5672"
      return 0
    fi

    sleep 1
  done

  echo "Timed out waiting for rabbitmq" >&2
  docker compose ps >&2
  exit 1
}

wait_for_rabbitmq
wait_for_url "monitor" "http://localhost:8080/health"
wait_for_url "plain-js-client" "http://localhost:8086/health"
wait_for_url "echo-service" "http://localhost:8082/health"
wait_for_url "weather-service" "http://localhost:8084/health"
wait_for_url "todo-service" "http://localhost:8088/health"

echo
echo "Stack is ready:"
echo "  Echo API:        http://localhost:8082"
echo "  Weather API:     http://localhost:8084"
echo "  Todo API:        http://localhost:8088"
echo "  Postgres:        localhost:5433"
echo "  RabbitMQ:        amqp://app:app@localhost:5672"
echo
echo "Useful checks:"
echo "  Monitor:         http://localhost:8080"
echo "  Plain JS Client: http://localhost:8086"
echo "  ToDo app         http://localhost:8087"
echo "  curl http://localhost:8082/echo/hello"
echo "  curl http://localhost:8084/weather/London"
echo "  curl http://localhost:8088/todos"
echo "  docker compose exec -T rabbitmq rabbitmq-diagnostics -q ping"

