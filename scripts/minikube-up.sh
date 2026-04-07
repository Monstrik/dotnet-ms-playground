#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd -- "$SCRIPT_DIR/.." && pwd)"
cd "$REPO_ROOT"

# ── 1. Start minikube ────────────────────────────────────────────────────────
echo "==> Starting minikube..."
minikube start --driver=docker --cpus=4 --memory=6144

# ── 2. Point Docker CLI at minikube's daemon ─────────────────────────────────
echo "==> Pointing Docker CLI at minikube's daemon..."
eval "$(minikube docker-env)"

# ── 3. Build all images inside minikube (no registry needed) ─────────────────
echo "==> Building images inside minikube..."
docker build -t echo-service:local            -f src/EchoService/Dockerfile           .
docker build -t weather-service:local         -f src/WeatherService/Dockerfile        .
docker build -t todo-service:local            -f src/TodoService/Dockerfile           .
docker build -t order-producer-service:local  -f src/OrderProducerService/Dockerfile  .
docker build -t kitchen-service:local         -f src/KitchenService/Dockerfile        .
docker build -t delivery-service:local        -f src/DeliveryService/Dockerfile       .
docker build -t monitor:local                 -f src/Monitor/Dockerfile               .
docker build -t plain-js-client:local         -f src/PlainJsClient/Dockerfile         .
docker build -t todo-app:local                -f src/TodoApp/Dockerfile               .

# ── 4. Apply manifests ────────────────────────────────────────────────────────
echo "==> Applying Kubernetes manifests..."
kubectl apply -f k8s/namespace.yaml

# Infrastructure first
kubectl apply -f k8s/rabbitmq.yaml
kubectl apply -f k8s/postgres.yaml

echo "==> Waiting for RabbitMQ to be ready (up to 3 min)..."
kubectl rollout status deployment/rabbitmq -n foodorder --timeout=180s

echo "==> Waiting for Postgres to be ready (up to 3 min)..."
kubectl rollout status deployment/postgres -n foodorder --timeout=180s

# Application services
kubectl apply -f k8s/echo-service.yaml
kubectl apply -f k8s/weather-service.yaml
kubectl apply -f k8s/todo-service.yaml
kubectl apply -f k8s/order-producer-service.yaml
kubectl apply -f k8s/kitchen-service.yaml
kubectl apply -f k8s/delivery-service.yaml
kubectl apply -f k8s/monitor.yaml
kubectl apply -f k8s/plain-js-client.yaml
kubectl apply -f k8s/todo-app.yaml

echo "==> Waiting for all application deployments..."
for svc in echo-service weather-service todo-service \
           order-producer-service kitchen-service delivery-service \
           monitor plain-js-client todo-app; do
  echo "    waiting for $svc..."
  kubectl rollout status deployment/$svc -n foodorder --timeout=180s
done

# ── 5. Start minikube tunnel for NodePort access ─────────────────────────────
echo ""
echo "==> Starting minikube tunnel (required for NodePort access)..."
echo "    This allows localhost:30XXX to reach the services."
minikube tunnel &
TUNNEL_PID=$!
sleep 2

# ── 6. Print access URLs ──────────────────────────────────────────────────────
MINIKUBE_IP=$(minikube ip)
echo ""
echo "╔══════════════════════════════════════════════════════════════════╗"
echo "║  ✓ All services are up and tunneled!                             ║"
echo "╠══════════════════════════════════════════════════════════════════╣"
echo "║  🌐 MONITOR DASHBOARD (localhost):                               ║"
echo "║     http://localhost:30080                                       ║"
echo "║                                                                  ║"
echo "║  OTHER SERVICES:                                                 ║"
echo "║    Plain JS client  →  http://localhost:30086                   ║"
echo "║    Todo app         →  http://localhost:30087                   ║"
echo "║    Order Producer   →  http://localhost:30091                   ║"
echo "║    Kitchen API      →  http://localhost:30092                   ║"
echo "║    Delivery API     →  http://localhost:30093                   ║"
echo "║                                                                  ║"
echo "║  (Or use minikube IP: http://$MINIKUBE_IP:30080)            ║"
echo "╠══════════════════════════════════════════════════════════════════╣"
echo "║  Tunnel is running in background (PID $TUNNEL_PID)              ║"
echo "║  To stop it: pkill -P $$ minikube || pkill -f 'minikube tunnel' ║"
echo "║  To stop all: ./scripts/minikube-down.sh                        ║"
echo "╚══════════════════════════════════════════════════════════════════╝"
echo ""

