#!/usr/bin/env bash
set -euo pipefail

echo "==> Stopping port-forwards..."
pkill -f "kubectl port-forward -n foodorder" || true
sleep 1

echo "==> Deleting foodorder namespace (removing all resources)..."
## Force delete all resources first
#kubectl delete all --all -n foodorder --ignore-not-found

## Force delete without waiting for graceful termination
#kubectl delete namespace foodorder --grace-period=0 --force --ignore-not-found || true

# Remove finalizers if namespace is stuck in Terminating state
echo "==> Removing finalizers from foodorder namespace (if needed)..."
kubectl get namespace foodorder -o json 2>/dev/null \
  | jq 'del(.spec.finalizers)' \
  | kubectl replace --raw "/api/v1/namespaces/foodorder/finalize" -f - || true

echo "==> Stopping minikube..."
minikube stop

echo "✓ Done. Cluster stopped."

