# Getting Started

Get up and running in 5 minutes. Choose your deployment method below.

## Prerequisites

- Docker & Docker Compose (for Docker setup)
- OR minikube + kubectl (for Kubernetes setup)
- A browser

## Quick Start

### Option A: Docker Compose (Easiest)

```bash
cd /Users/ayakovis/RiderProjects/Solution2

# Start all services
./scripts/dev-up.sh

# Wait for readiness (~2 min on first run)
# Then open in browser
open http://localhost:8080
```

**Access points:**
- Monitor Dashboard: `http://localhost:8080`
- Order Producer API: `http://localhost:8091`
- Kitchen API: `http://localhost:8092`
- Delivery API: `http://localhost:8093`

**Stop everything:**
```bash
./scripts/dev-down.sh
```

---

### Option B: Kubernetes with Minikube (Production-like)

**Terminal 1: Provision cluster**
```bash
./scripts/minikube-up.sh
# Waits for readiness (~2-3 min first run)
```

**Terminal 2: Establish localhost access** (keep running)
```bash
./scripts/minikube-access.sh
# Keep this open - provides localhost:8080/8091/8092/8093 access
```

**Then in browser:**
```bash
open http://localhost:8080
```

**Stop everything:**
```bash
# Press Ctrl+C in Terminal 2
./scripts/minikube-down.sh
```

---

## Demo the Workflow

1. **Open** `http://localhost:8080` (Monitor Dashboard)

2. **Start services** by clicking:
   - `Order Producer` > **Start** (generates 12 orders/min)
   - `Kitchen` > **Start** (prepares 5-30 seconds)
   - `Delivery` > **Start** (delivers 30-60 seconds)

3. **Watch orders flow:**
   - New Orders → In Kitchen → Ready → Out for Delivery → Delivered
   - Live counts update at top of page
   - Each order shows elapsed time in current stage

4. **Adjust configuration at runtime:**
   - Change "Orders per minute" → Click "Apply config"
   - Change prep time range → Click "Apply config"
   - Change delivery time range → Click "Apply config"

5. **Pause a stage:**
   - Click any service's **Stop** button
   - Orders pile up in that queue
   - Click **Start** to resume

---

## Dashboard Features

**Workflow Status Summary (top of page)**
- New Orders: count
- In the Kitchen: count
- Ready for Delivery: count
- Out for Delivery: count
- Delivered: count

**Service Controls**
- Order Producer: Start/Stop + "Orders per minute" config
- Kitchen: Start/Stop + prep time range config
- Delivery: Start/Stop + delivery time range config

**Order Status Tables** (5 separate tables)
- Order ID, Created timestamp, Elapsed time, Items

**Queue Metrics**
- orders.new message count
- orders.readyForDelivery message count
- orders.delivered message count

**Admin**
- Reset all queues button
- Refresh now button

---

## Troubleshooting

### Can't access http://localhost:8080

**Docker Compose:**
```bash
docker ps          # verify containers running
curl http://localhost:8080/health
```

**Kubernetes:**
- Ensure `./scripts/minikube-access.sh` is running in another terminal
- Check: `curl http://localhost:8080/health`

### Orders aren't flowing

1. Verify all 3 services show "Running" (green badges)
2. Check queue metrics for message buildup
3. Try "Reset all queues" button
4. Restart services

### Services won't start on Kubernetes

```bash
# Check RabbitMQ
kubectl logs -n foodorder deployment/rabbitmq

# Check Postgres
kubectl logs -n foodorder deployment/postgres

# Check general status
kubectl get pods -n foodorder
```

### Port 8080 already in use

```bash
# Find process using port
lsof -i :8080

# Kill it
kill -9 <PID>
```

---

## API Examples

### Get Current Configuration
```bash
curl http://localhost:8091/config
curl http://localhost:8092/config
curl http://localhost:8093/config
```

### Update Configuration
```bash
# Update orders per minute
curl -X PUT http://localhost:8091/config \
  -H 'Content-Type: application/json' \
  -d '{"ordersPerMinute": 20}'

# Update kitchen prep time (seconds)
curl -X PUT http://localhost:8092/config \
  -H 'Content-Type: application/json' \
  -d '{"minPreparationSeconds": 3, "maxPreparationSeconds": 15}'

# Update delivery time (seconds)
curl -X PUT http://localhost:8093/config \
  -H 'Content-Type: application/json' \
  -d '{"minDeliverySeconds": 10, "maxDeliverySeconds": 30}'
```

### Control Services
```bash
# Start Order Producer
curl -X POST http://localhost:8091/control/start

# Stop Kitchen
curl -X POST http://localhost:8092/control/stop

# Get service stats
curl http://localhost:8091/stats
curl http://localhost:8092/stats
curl http://localhost:8093/stats
```

---

## Script Reference

| Script | Purpose |
|--------|---------|
| `./scripts/dev-up.sh` | Start Docker Compose (all services) |
| `./scripts/dev-down.sh` | Stop Docker Compose |
| `./scripts/minikube-up.sh` | Provision Kubernetes cluster |
| `./scripts/minikube-down.sh` | Tear down Kubernetes |
| `./scripts/minikube-access.sh` | Establish localhost tunnels (for Kubernetes) |

---

## Architecture

```
Order Producer (8091)
    ↓
orders.new (RabbitMQ queue)
    ↓
Kitchen Service (8092)
    ↓
orders.readyForDelivery (RabbitMQ queue)
    ↓
Delivery Service (8093)
    ↓
orders.delivered (RabbitMQ queue)
    ↓
Monitor Dashboard (8080) - displays everything
```

---

## What's Running

### Docker Compose (11 containers)
- echo-service (8082)
- weather-service (8084)
- todo-service (8088, uses postgres)
- order-producer-service (8091)
- kitchen-service (8092)
- delivery-service (8093)
- monitor (8080)
- plain-js-client (8086)
- todo-app (8087)
- rabbitmq (5672)
- postgres (5433)

### Kubernetes (same services in pods)
- All services accessible on localhost via minikube-access.sh
- Same port numbers for compatibility
- RabbitMQ and Postgres run as internal cluster services

---

## Next Steps

1. **Read README.md** for full architecture and all available endpoints
2. **Read AGENTS.md** for development conventions
3. **Modify the code** - services are minimal APIs, easy to extend
4. **Deploy to production K8s** - all manifests in `k8s/` are production-ready

---

**Questions?** See README.md or inline comments in scripts and code.

