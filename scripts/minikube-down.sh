#!/usr/bin/env bash
set -euo pipefail

echo "==> Stopping minikube tunnel..."
pkill -f "minikube tunnel" || true
sleep 1

echo "==> Deleting foodorder namespace (removes all resources)..."
kubectl delete namespace foodorder --ignore-not-found

echo "==> Stopping minikube..."
minikube stop

echo "Done."

