#!/usr/bin/env bash
set -euo pipefail

# Reliable local access for Docker-driver minikube on macOS.
# Keeps port-forwards open so browser can reach all UIs/APIs on localhost.

start_forward() {
  local name="$1"
  local svc="$2"
  local local_port="$3"
  local remote_port="$4"

  kubectl port-forward -n foodorder "svc/$svc" "$local_port:$remote_port" \
    >/tmp/"$name"-pf.log 2>&1 &
  PIDS+=("$!")
}

cleanup() {
  for pid in "${PIDS[@]:-}"; do
    kill "$pid" >/dev/null 2>&1 || true
  done
}

trap cleanup EXIT INT TERM

kubectl get ns foodorder >/dev/null

# Stop old port-forwards to avoid port collisions.
pkill -f "kubectl port-forward -n foodorder svc/monitor 8080:8080" >/dev/null 2>&1 || true
pkill -f "kubectl port-forward -n foodorder svc/plain-js-client 8086:80" >/dev/null 2>&1 || true
pkill -f "kubectl port-forward -n foodorder svc/todo-app 8087:80" >/dev/null 2>&1 || true
pkill -f "kubectl port-forward -n foodorder svc/order-producer-service 8091:8080" >/dev/null 2>&1 || true
pkill -f "kubectl port-forward -n foodorder svc/kitchen-service 8092:8080" >/dev/null 2>&1 || true
pkill -f "kubectl port-forward -n foodorder svc/delivery-service 8093:8080" >/dev/null 2>&1 || true

PIDS=()
start_forward monitor monitor 8080 8080
start_forward plain-js-client plain-js-client 8086 80
start_forward todo-app todo-app 8087 80
start_forward order-producer order-producer-service 8091 8080
start_forward kitchen kitchen-service 8092 8080
start_forward delivery delivery-service 8093 8080

sleep 2

curl -fsS http://localhost:8080/ >/dev/null

echo "Local access is ready:"
echo "  Monitor:         http://localhost:8080"
echo "  Plain JS Client: http://localhost:8086"
echo "  Todo App:        http://localhost:8087"
echo
echo "Workflow APIs (for monitor query params if needed):"
echo "  Order Producer:  http://localhost:8091"
echo "  Kitchen:         http://localhost:8092"
echo "  Delivery:        http://localhost:8093"
echo
echo "Press Ctrl+C to stop all forwards."

wait

