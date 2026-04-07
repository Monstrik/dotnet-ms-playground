#!/usr/bin/env bash
set -euo pipefail

echo "==> Stopping port-forwards..."
pkill -f "kubectl port-forward -n foodorder" || true
sleep 1

echo "==> Deleting foodorder namespace (removing all resources)..."
# Force delete without waiting for graceful termination
kubectl delete namespace foodorder --grace-period=0 --force --ignore-not-found

echo "==> Stopping minikube..."
minikube stop

echo "✓ Done. Cluster stopped."

