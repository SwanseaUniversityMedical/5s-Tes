#!/bin/bash
# Creates the "dev-only-token" that IDE-run apps use against one namespace's
# runtime Vault. Init, unseal, the "secret" mount and the in-cluster app
# token are all handled by the stack's vault-init CronJob; this script only
# waits for that job's keys Secret and mints the fixed dev token
# (`vault token create -id` can choose a token id, the job's random app
# token cannot be known in advance by IDE launch profiles).
#
# Usage: vault-init.sh <namespace> <kube-context> <vault-release-name>
# <vault-release-name> is submission-vault/agent-vault; its pod is
# <vault-release-name>-0 and its keys Secret <vault-release-name>-keys.
set -euo pipefail

NAMESPACE="${1:?usage: vault-init.sh <namespace> <kube-context> <vault-release-name>}"
CONTEXT="${2:?usage: vault-init.sh <namespace> <kube-context> <vault-release-name>}"
VAULT_RELEASE="${3:?usage: vault-init.sh <namespace> <kube-context> <vault-release-name>}"
VAULT_POD="${VAULT_RELEASE}-0"
KEYS_SECRET="${VAULT_RELEASE}-keys"

echo "Vault ($NAMESPACE): waiting for the $KEYS_SECRET Secret from the ${VAULT_RELEASE}-init CronJob"
# The CronJob runs every 5 minutes and needs the vault pod up first, so the
# first run after cluster creation can take a while.
elapsed=0
until kubectl get secret "$KEYS_SECRET" -n "$NAMESPACE" --context "$CONTEXT" >/dev/null 2>&1; do
  if [ "$elapsed" -ge 600 ]; then
    echo "$KEYS_SECRET did not appear in $NAMESPACE within 600s." >&2
    echo "Check: kubectl -n $NAMESPACE get cronjob ${VAULT_RELEASE}-init; kubectl -n $NAMESPACE get pods" >&2
    exit 1
  fi
  sleep 10; elapsed=$((elapsed + 10))
done

ROOT_TOKEN=$(kubectl get secret "$KEYS_SECRET" -n "$NAMESPACE" --context "$CONTEXT" \
  -o jsonpath='{.data.root_token}' | base64 -d)

vault_authed() {
  kubectl exec -i "$VAULT_POD" -n "$NAMESPACE" --context "$CONTEXT" -- sh -c "VAULT_TOKEN='$ROOT_TOKEN' $1"
}

if vault_authed "vault token lookup dev-only-token" >/dev/null 2>&1; then
  echo "Vault ($NAMESPACE): 'dev-only-token' already exists"
else
  echo "Vault ($NAMESPACE): creating the 'dev-only-token' for IDE-run apps"
  vault_authed "vault token create -id=dev-only-token -policy=root -orphan" >/dev/null
fi

echo "Vault ($NAMESPACE): ready"
