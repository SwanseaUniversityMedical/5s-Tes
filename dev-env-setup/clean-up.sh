#!/bin/bash
set -euo pipefail
cd "$(dirname "$0")"

CLUSTER_NAME="5s-tes"

if kind get clusters 2>/dev/null | grep -qx "$CLUSTER_NAME"; then
  kind delete cluster --name "$CLUSTER_NAME"
else
  echo "No \"$CLUSTER_NAME\" kind cluster to delete."
fi

# Legacy keys files from before the in-cluster vault-init CronJob owned them.
rm -f .vault-keys-5s-tes-submission .vault-keys-5s-tes-agent
echo "Run ./cluster-setup.sh for a fresh environment."
