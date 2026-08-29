#!/bin/bash
# Initialises, unseals, and configures one namespace's own runtime Vault
# (server.standalone, not dev mode - see charts/*-stack/README.md "Vault").
# Unlike Director-Airlock's dev Vault (auto-unsealed, devstack-owned), ours
# is the stack's own prod-mode Vault (Decision 5): it starts sealed and
# needs real init/unseal every time the cluster is rebuilt.
#
# Locally vault.secretsEnabled=false, so the operator-read kvv2 mount and
# Kubernetes auth (Airlock's configure_vault) are not needed - nothing here
# reads through the redhat-cop vault-config-operator. Only two things are
# needed for the apps to work:
#   1. the "secret" kv-v2 mount apps read/write at runtime
#      (VaultCredentialsService, api.vault.secretEngine default "secret")
#   2. a real Vault token equal to the static Secrets' "dev-only-token"
#      (submission-api-secret/agent-api-secret/credentials-camunda-secret's
#      vaultToken) - created as a named, root-policy child token of the
#      real (random) root token init produces, since `vault operator init`
#      cannot choose the root token's own ID.
#
# Usage: vault-init.sh <namespace> <kube-context> <vault-release-name>
# <vault-release-name> is submission-vault/agent-vault (R33: distinct per
# family, since the hashicorp/vault chart's cluster-scoped ClusterRoleBinding
# is named from the release name alone - see charts/*-stack/templates/vault.yaml).
# Its StatefulSet's pod is <vault-release-name>-0.
set -euo pipefail

NAMESPACE="${1:?usage: vault-init.sh <namespace> <kube-context> <vault-release-name>}"
CONTEXT="${2:?usage: vault-init.sh <namespace> <kube-context> <vault-release-name>}"
VAULT_RELEASE="${3:?usage: vault-init.sh <namespace> <kube-context> <vault-release-name>}"
VAULT_POD="${VAULT_RELEASE}-0"
KEYS_FILE="$(cd "$(dirname "$0")" && pwd)/.vault-keys-${NAMESPACE}"

echo "Vault ($NAMESPACE): waiting for $VAULT_POD to exist"
# `kubectl wait` errors immediately on a not-yet-created resource rather
# than waiting for it - ArgoCD needs a moment after `helm install` to sync
# the vault Application's StatefulSet into existence.
elapsed=0
until kubectl get "pod/$VAULT_POD" -n "$NAMESPACE" --context "$CONTEXT" >/dev/null 2>&1; do
  if [ "$elapsed" -ge 120 ]; then
    echo "$VAULT_POD did not appear in $NAMESPACE within 120s. Check: kubectl -n $NAMESPACE get application $VAULT_RELEASE -o yaml" >&2
    exit 1
  fi
  sleep 5; elapsed=$((elapsed + 5))
done

echo "Vault ($NAMESPACE): waiting for the vault container to be execable"
# Not --for=condition=Ready: the chart's readinessProbe runs "vault status",
# which fails (by design) while sealed - a brand-new pod never reaches
# Ready on its own. condition=Initialized only means init containers are
# done, not that the main "vault" container has actually started yet
# (`kubectl exec` fails with "container not found" until it has) - so poll
# with the same command this script needs to succeed.
elapsed=0
until kubectl exec "$VAULT_POD" -n "$NAMESPACE" --context "$CONTEXT" -- true >/dev/null 2>&1; do
  if [ "$elapsed" -ge 300 ]; then
    echo "$VAULT_POD's vault container did not start in $NAMESPACE within 300s. Check: kubectl -n $NAMESPACE describe pod $VAULT_POD" >&2
    exit 1
  fi
  sleep 5; elapsed=$((elapsed + 5))
done

vault_exec() {
  kubectl exec -i "$VAULT_POD" -n "$NAMESPACE" --context "$CONTEXT" -- sh -c "$1"
}

STATUS_JSON=$(vault_exec "vault status -format=json" || true)
INITIALIZED=$(printf '%s' "$STATUS_JSON" | jq -r '.initialized // false')

if [ "$INITIALIZED" != "true" ]; then
  echo "Vault ($NAMESPACE): initialising (1 key share, threshold 1 - local dev only)"
  INIT_JSON=$(vault_exec "vault operator init -key-shares=1 -key-threshold=1 -format=json")
  printf '%s\n' "$INIT_JSON" > "$KEYS_FILE"
  chmod 600 "$KEYS_FILE"
  echo "Vault ($NAMESPACE): unseal key + root token saved to $KEYS_FILE (gitignored)"
fi

if [ ! -f "$KEYS_FILE" ]; then
  echo "Vault ($NAMESPACE) is already initialised but $KEYS_FILE is missing." >&2
  echo "This Vault cannot be unsealed by this script. Either restore the keys file" >&2
  echo "or delete the Vault Application's PVC and let it re-init." >&2
  exit 1
fi

UNSEAL_KEY=$(jq -r '.unseal_keys_b64[0]' "$KEYS_FILE")
ROOT_TOKEN=$(jq -r '.root_token' "$KEYS_FILE")

STATUS_JSON=$(vault_exec "vault status -format=json" || true)
SEALED=$(printf '%s' "$STATUS_JSON" | jq -r '.sealed // true')
if [ "$SEALED" = "true" ]; then
  echo "Vault ($NAMESPACE): unsealing"
  vault_exec "vault operator unseal '$UNSEAL_KEY'" >/dev/null
fi

vault_authed() {
  kubectl exec -i "$VAULT_POD" -n "$NAMESPACE" --context "$CONTEXT" -- sh -c "VAULT_TOKEN='$ROOT_TOKEN' $1"
}

if ! vault_authed "vault secrets list -format=json" | jq -e 'has("secret/")' >/dev/null; then
  echo "Vault ($NAMESPACE): enabling the 'secret' kv-v2 mount (app runtime store)"
  vault_authed "vault secrets enable -path=secret -version=2 kv" >/dev/null
else
  echo "Vault ($NAMESPACE): 'secret' mount already enabled"
fi

if vault_authed "vault token lookup dev-only-token" >/dev/null 2>&1; then
  echo "Vault ($NAMESPACE): 'dev-only-token' already exists"
else
  echo "Vault ($NAMESPACE): creating the 'dev-only-token' the static Secrets carry"
  vault_authed "vault token create -id=dev-only-token -policy=root -orphan" >/dev/null
fi

echo "Vault ($NAMESPACE): ready"
