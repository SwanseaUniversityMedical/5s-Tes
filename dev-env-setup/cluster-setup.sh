#!/bin/bash
set -Eeuo pipefail

on_error() {
  local code=$? line="$1"
  echo >&2
  echo "===============================================================================" >&2
  echo " cluster-setup.sh STOPPED at line $line (exit code $code)" >&2
  echo >&2
  echo " Run it again with tracing to see the failing command:" >&2
  echo "   bash -x $0 2>&1 | tail -40" >&2
  echo >&2
  echo " Re-running this script picks up where it left off (idempotent) - the kind cluster" >&2
  echo " and everything already installed is reused. Only if a PARTIALLY created kind" >&2
  echo " cluster itself looks broken (e.g. it exists but core components never came up)," >&2
  echo " run ./clean-up.sh first to delete it and start clean." >&2
  echo "===============================================================================" >&2
  exit "$code"
}
trap 'on_error "$LINENO"' ERR

# kind doesn't work on apple silicon if this is set
unset DOCKER_DEFAULT_PLATFORM

CONTEXT="kind-5s-tes"
CLUSTER_NAME="5s-tes"
SUBMISSION_NS="5s-tes-submission"
AGENT_NS="5s-tes-agent"

# renovate: datasource=helm depName=ingress-nginx registryUrl=https://kubernetes.github.io/ingress-nginx
INGRESS_NGINX_CHART_VERSION="4.15.1"
# renovate: datasource=helm depName=cert-manager registryUrl=https://charts.jetstack.io
CERT_MANAGER_VERSION="v1.21.1"
# renovate: datasource=helm depName=argo-cd registryUrl=https://argoproj.github.io/argo-helm
ARGOCD_CHART_VERSION="10.4.0"
# renovate: datasource=helm depName=cloudnative-pg registryUrl=https://cloudnative-pg.github.io/charts
CNPG_CHART_VERSION="0.29.0"

cd "$(dirname "$0")"
REPO_ROOT="$(cd .. && pwd)"

echo "5S-TES dev env setup"
echo "  platform : $(uname -s)"
echo "  shell    : bash $BASH_VERSION"
echo "  directory: $(pwd)"

###############################################################################
# Preflight: the tools this script drives must be installed
###############################################################################

require_tools() {
  local missing="" tool
  for tool in docker kind kubectl helm curl jq vault; do
    command -v "$tool" >/dev/null 2>&1 || missing="$missing $tool"
  done

  [ -z "$missing" ] && return 0

  echo >&2
  echo "===============================================================================" >&2
  echo " These tools are not on your PATH:$missing" >&2
  echo "===============================================================================" >&2
  exit 1
}

require_tools

CLUSTER_EXISTS=0
if kind get clusters 2>/dev/null | grep -qx "$CLUSTER_NAME"; then
  CLUSTER_EXISTS=1
  echo
  echo "Cluster \"$CLUSTER_NAME\" is already there. Reusing it (idempotent re-run)."
  echo "Run ./clean-up.sh first if you want it built again from scratch."
fi

# Point in-cluster pods resolving each family's keycloak.<host> at the
# ingress controller, not its Service directly: server-side OIDC calls (api,
# ui, web, and RustFS's OIDC discovery) need the ingress-routed host, and
# *.localtest.me otherwise resolves to loopback inside every pod too.
apply_coredns() {
  kubectl apply -f files/deps/coredns.yaml --context "$CONTEXT"
  kubectl rollout restart deployment/coredns -n kube-system --context "$CONTEXT"
  kubectl rollout status deployment/coredns -n kube-system --timeout=2m --context "$CONTEXT"
}

show_unhealthy_pods() {
  local ns
  for ns in "$SUBMISSION_NS" "$AGENT_NS"; do
    echo
    echo "Pods in $ns that are not Running or Completed:"
    kubectl get pods -n "$ns" --context "$CONTEXT" --no-headers 2>/dev/null \
      | awk '$3 != "Running" && $3 != "Completed" { print "  " $0 }' || true
    echo "  (nothing listed above means every pod is fine)"
  done
}

# wait_for_argocd_apps <namespace> <friendly-label>
# Every Application this bootstrap creates lives in its own family
# namespace (unlike Director-Airlock, nothing here needs a separate
# monitoring/vault namespace) - so this only has to look in one place.
wait_for_argocd_apps() {
  local ns="$1" label="$2"
  local timeout="${ARGO_WAIT_TIMEOUT:-2400}" poll=15 stable_needed=3
  local deadline app_rows total not_ready stable=0 last_total=-1

  deadline=$(( $(date +%s) + timeout ))
  echo
  echo "Waiting for ArgoCD to sync $label ($ns). A first run pulls every image,"
  echo "so allow up to $(( timeout / 60 )) minutes."

  while [ "$(date +%s)" -lt "$deadline" ]; do
    app_rows=$(kubectl get applications.argoproj.io -n "$ns" --context "$CONTEXT" \
      -o jsonpath='{range .items[*]}{.metadata.name}{"|"}{.status.sync.status}{"|"}{.status.health.status}{"\n"}{end}' \
      2>/dev/null || true)

    total=$(printf '%s\n' "$app_rows" | grep -c . || true)
    : "${total:=0}"

    if [ "$total" -eq 0 ]; then
      echo "  waiting for ArgoCD to create Applications in $ns"
      stable=0; last_total=-1
      sleep "$poll"
      continue
    fi

    # Gate on Health only, not Sync: submission's and agent's "vault"
    # Applications both install the hashicorp/vault chart under the release
    # name "vault", whose ClusterRoleBinding "vault-server-binding" is
    # CLUSTER-scoped and named from {{ vault.fullname }} alone (no
    # namespace) - the two releases fight over that one object forever
    # (ArgoCD "SharedResourceWarning"), so one Application's sync.status
    # never leaves OutOfSync even though Vault itself runs fine (nothing
    # locally uses vault's Kubernetes-auth path this depends on -
    # vault.secretsEnabled=false). See README "Known limitations".
    not_ready=$(printf '%s\n' "$app_rows" | awk -F'|' 'NF && $3 != "Healthy"')

    if [ -z "$not_ready" ] && [ "$total" -eq "$last_total" ]; then
      stable=$(( stable + 1 ))
      if [ "$stable" -ge "$stable_needed" ]; then
        echo "  all $total Applications in $ns are Healthy"
        printf '%s\n' "$app_rows" | awk -F'|' '$2 != "Synced" {printf "  note: %s is Healthy but sync=%s (see README)\n", $1, $2}'
        return 0
      fi
      echo "  all $total Applications healthy - confirming ($stable/$stable_needed)"
    else
      stable=0
      echo "  still working, $total Applications in $ns:"
      printf '%s\n' "$app_rows" | awk -F'|' \
        '{printf "    %-24s sync=%-12s health=%s\n", $1, ($2 == "" ? "-" : $2), ($3 == "" ? "-" : $3)}'
    fi

    last_total="$total"
    sleep "$poll"
  done

  echo
  echo "$label ($ns) did not become fully healthy in $(( timeout / 60 )) minutes." >&2
  return 1
}

# wait_for_cnpg_ready <namespace>
wait_for_cnpg_ready() {
  local ns="$1" elapsed=0 timeout=120
  echo "Waiting for the postgres Cluster's pod(s) in $ns (up to 10m)"
  until [ -n "$(kubectl -n "$ns" get pod -l cnpg.io/cluster=postgres,cnpg.io/podRole=instance \
      -o name --context "$CONTEXT" 2>/dev/null)" ]; do
    if [ "$elapsed" -ge "$timeout" ]; then
      echo "No pods appeared for the postgres Cluster in $ns within ${timeout}s. Check: kubectl -n $ns describe cluster postgres" >&2
      exit 1
    fi
    sleep 5; elapsed=$((elapsed + 5))
  done
  kubectl -n "$ns" wait --for=condition=Ready pod -l cnpg.io/cluster=postgres,cnpg.io/podRole=instance \
    --timeout=600s --context "$CONTEXT"
  echo "postgres ($ns) is Ready."
}

# wait_for_rabbitmq_ready <namespace>
# The RabbitMQ Cluster Operator names the StatefulSet "<RabbitmqCluster
# name>-server" - our RabbitmqCluster is named "rabbitmq" (see
# charts/*-stack/templates/rabbitmq.yaml), so this is a static name, not a
# guessed label.
wait_for_rabbitmq_ready() {
  local ns="$1" elapsed=0 timeout=180
  echo "Waiting for the rabbitmq-server StatefulSet in $ns (up to 10m)"
  until kubectl -n "$ns" get statefulset rabbitmq-server --context "$CONTEXT" >/dev/null 2>&1; do
    if [ "$elapsed" -ge "$timeout" ]; then
      echo "No rabbitmq-server StatefulSet appeared in $ns within ${timeout}s. Check: kubectl -n $ns describe rabbitmqcluster rabbitmq" >&2
      exit 1
    fi
    sleep 5; elapsed=$((elapsed + 5))
  done
  kubectl -n "$ns" rollout status statefulset/rabbitmq-server \
    --timeout=600s --context "$CONTEXT"
  echo "rabbitmq ($ns) is Ready."
}

###############################################################################
# Local app images: nothing is published to Harbor yet (see README), so the
# five C# components and agent-web are built from this working tree and
# loaded straight into kind's containerd - no registry involved.
###############################################################################

build_and_load_images() {
  echo
  echo "Building local app images from the working tree"
  docker build -q -f "$REPO_ROOT/Submission/Submission.Api/Dockerfile" -t 5s-tes/submission-api:local "$REPO_ROOT"
  docker build -q -f "$REPO_ROOT/Submission/Submission.Web/Dockerfile" -t 5s-tes/submission-ui:local "$REPO_ROOT"
  docker build -q -f "$REPO_ROOT/Agent/Agent.Api/Dockerfile" -t 5s-tes/agent-api:local "$REPO_ROOT"
  docker build -q -f "$REPO_ROOT/Agent/Agent.Web/Dockerfile" -t 5s-tes/agent-ui:local "$REPO_ROOT"
  docker build -q -f "$REPO_ROOT/Credentials/Credentials.Camunda/Dockerfile" -t 5s-tes/credentials-camunda:local "$REPO_ROOT"
  docker build -q -t 5s-tes/agent-web:local "$REPO_ROOT/Agent/agent-web"

  echo "Loading local app images into kind"
  kind load docker-image \
    5s-tes/submission-api:local \
    5s-tes/submission-ui:local \
    5s-tes/agent-api:local \
    5s-tes/agent-ui:local \
    5s-tes/agent-web:local \
    5s-tes/credentials-camunda:local \
    --name "$CLUSTER_NAME"
}

if [ "$CLUSTER_EXISTS" = "0" ]; then
  kind create cluster --config=kind-config.yaml

  # kind's "standard" StorageClass (local-path-provisioner) is RWO-only by
  # default. sharedFileSystemPath makes it serve RWX claims too, which
  # submission.dataProtection/agent.processModels need. Single-node only.
  # https://github.com/kubernetes-sigs/kind/issues/1487#issuecomment-2211072952
  echo "Enabling RWX support on kind's local-path-provisioner"
  kubectl wait --for=condition=Available deployment/local-path-provisioner \
    -n local-path-storage --timeout=2m --context "$CONTEXT"
  kubectl -n local-path-storage patch configmap local-path-config --type merge \
    -p '{"data":{"config.json":"{\n\"sharedFileSystemPath\": \"/var/local-path-provisioner\"\n}"}}' \
    --context "$CONTEXT"
  kubectl -n local-path-storage rollout restart deployment/local-path-provisioner \
    --context "$CONTEXT"
  kubectl -n local-path-storage rollout status deployment/local-path-provisioner \
    --timeout=2m --context "$CONTEXT"

  echo "Installing ingress-nginx ($INGRESS_NGINX_CHART_VERSION)"
  helm upgrade --install ingress-nginx ingress-nginx \
    --repo https://kubernetes.github.io/ingress-nginx \
    --version "$INGRESS_NGINX_CHART_VERSION" \
    --namespace ingress-nginx --create-namespace \
    -f files/deps/ingress-nginx.yaml --kube-context "$CONTEXT"
  kubectl wait --for=condition=Available deployment/ingress-nginx-controller \
    -n ingress-nginx --timeout=5m --context "$CONTEXT"

  apply_coredns

  echo "Installing cert-manager ($CERT_MANAGER_VERSION)"
  helm upgrade --install cert-manager cert-manager \
    --repo https://charts.jetstack.io \
    --version "$CERT_MANAGER_VERSION" \
    --namespace cert-manager --create-namespace \
    --set crds.enabled=true --kube-context "$CONTEXT"
  kubectl wait --for=condition=Available deployment --all \
    -n cert-manager --timeout=5m --context "$CONTEXT"

  echo "Applying self-signed ClusterIssuer 'ca-issuer' (global.ingress.certClusterIssuer default)"
  cat <<'EOF' | kubectl apply --context "$CONTEXT" -f -
apiVersion: cert-manager.io/v1
kind: ClusterIssuer
metadata:
  name: ca-issuer
spec:
  selfSigned: {}
EOF

  ###############################################################################
  # Operators - installed by this script only, never by a chart. The same
  # ones production runs, so the stack charts' operator objects behave
  # identically here. RabbitMQ's operator manifest needs cert-manager for
  # its webhook certificates (already installed above).
  ###############################################################################

  echo "Installing the CloudNativePG operator ($CNPG_CHART_VERSION)"
  helm upgrade --install cnpg cloudnative-pg \
    --repo https://cloudnative-pg.github.io/charts \
    --version "$CNPG_CHART_VERSION" \
    --namespace cnpg-system --create-namespace --kube-context "$CONTEXT"
  kubectl wait --for=condition=Available deployment --all \
    -n cnpg-system --timeout=5m --context "$CONTEXT"

  echo "Installing the RabbitMQ Cluster Operator"
  kubectl apply --context "$CONTEXT" \
    -f "https://github.com/rabbitmq/cluster-operator/releases/latest/download/cluster-operator.yml"
  kubectl wait --for=condition=Available deployment --all \
    -n rabbitmq-system --timeout=5m --context "$CONTEXT"

  echo "Installing ArgoCD ($ARGOCD_CHART_VERSION)"
  helm upgrade --install argocd argo-cd \
    --repo https://argoproj.github.io/argo-helm \
    --version "$ARGOCD_CHART_VERSION" \
    --namespace argocd --create-namespace \
    -f files/deps/argo.yaml --kube-context "$CONTEXT"
  kubectl wait --for=condition=Available deployment/argocd-server \
    -n argocd --timeout=10m --context "$CONTEXT"
  kubectl wait --for=condition=Available deployment/argocd-repo-server \
    -n argocd --timeout=10m --context "$CONTEXT"
else
  apply_coredns
fi

echo "Applying ArgoCD AppProjects and the Bitnami OCI repo registration"
kubectl apply -f files/argo/submission-project.yaml --context "$CONTEXT"
kubectl apply -f files/argo/agent-project.yaml --context "$CONTEXT"
kubectl apply -f files/argo/repo.yaml --context "$CONTEXT"

build_and_load_images

###############################################################################
# Install order: each family's devstack (local Keycloak/dev realm, Vault,
# Adminer, static Secrets), then its stack (dependencies + VaultSecrets,
# submission/agent Applications disabled - see submission-stack-local.yaml).
# All six charts installed from the working tree (`../charts/...`), not
# Harbor - nothing is published yet. See README.
###############################################################################

kubectl create namespace "$SUBMISSION_NS" --dry-run=client -o yaml | kubectl apply --context "$CONTEXT" -f - >/dev/null
kubectl create namespace "$AGENT_NS" --dry-run=client -o yaml | kubectl apply --context "$CONTEXT" -f - >/dev/null

echo "Installing submission-devstack"
helm upgrade --install submission-devstack ../charts/submission-devstack \
  --namespace "$SUBMISSION_NS" -f files/values/submission-devstack-local.yaml --kube-context "$CONTEXT"

echo "Installing submission-stack"
helm upgrade --install submission-stack ../charts/submission-stack \
  --namespace "$SUBMISSION_NS" -f files/values/submission-stack-local.yaml --kube-context "$CONTEXT"

echo "Installing agent-devstack"
helm upgrade --install agent-devstack ../charts/agent-devstack \
  --namespace "$AGENT_NS" -f files/values/agent-devstack-local.yaml --kube-context "$CONTEXT"

echo "Installing agent-stack"
helm upgrade --install agent-stack ../charts/agent-stack \
  --namespace "$AGENT_NS" -f files/values/agent-stack-local.yaml --kube-context "$CONTEXT"

###############################################################################
# Vault: each family's own runtime Vault starts sealed (prod mode -
# Decision 5), so its Application never reports ArgoCD-Healthy (the chart's
# readinessProbe runs "vault status", which fails while sealed) until this
# runs. Must happen before the health wait below, not after.
###############################################################################

./vault-init.sh "$SUBMISSION_NS" "$CONTEXT"
./vault-init.sh "$AGENT_NS" "$CONTEXT"

DEPS_HEALTHY=1
wait_for_argocd_apps "$SUBMISSION_NS" "submission dependencies" || DEPS_HEALTHY=0
wait_for_argocd_apps "$AGENT_NS" "agent dependencies" || DEPS_HEALTHY=0

if [ "$DEPS_HEALTHY" = "0" ]; then
  show_unhealthy_pods
  echo >&2
  echo "===============================================================================" >&2
  echo " STOPPING. Dependencies are not healthy yet." >&2
  echo " Open http://argocd.localtest.me and find the Application that is not green." >&2
  echo " Then run this script again - it keeps the cluster and only waits again." >&2
  echo "===============================================================================" >&2
  exit 1
fi

wait_for_cnpg_ready "$SUBMISSION_NS"
wait_for_cnpg_ready "$AGENT_NS"
wait_for_rabbitmq_ready "$SUBMISSION_NS"
wait_for_rabbitmq_ready "$AGENT_NS"

###############################################################################
# The product charts themselves - installed directly, standing in for the
# submission-stack/agent-stack Applications that are disabled locally.
###############################################################################

echo "Installing submission"
helm upgrade --install submission ../charts/submission \
  --namespace "$SUBMISSION_NS" -f files/values/submission-product-local.yaml --kube-context "$CONTEXT"

echo "Installing agent"
helm upgrade --install agent ../charts/agent \
  --namespace "$AGENT_NS" -f files/values/agent-product-local.yaml --kube-context "$CONTEXT"

echo
echo "Waiting for the submission/agent Deployments to become Available (up to 10m)"
kubectl -n "$SUBMISSION_NS" wait --for=condition=Available deployment --all --timeout=600s --context "$CONTEXT"
kubectl -n "$AGENT_NS" wait --for=condition=Available deployment --all --timeout=600s --context "$CONTEXT"

show_unhealthy_pods

cat <<SUMMARY

===============================================================================
 5S-TES local dev environment is up.

 Submission (ns $SUBMISSION_NS):
   UI            http://submission.submission.localtest.me
   API           http://submission-api.submission.localtest.me
   Keycloak      http://keycloak.submission.localtest.me (admin/admin, realm Dare-Control)
   Adminer       http://adminer.submission.localtest.me
   Seq           http://seq.submission.localtest.me
   RustFS console http://rustfs.submission.localtest.me

 Agent (ns $AGENT_NS):
   Web (UI)      http://agent.agent.localtest.me
   Legacy UI     http://agent-ui.agent.localtest.me
   API           http://agent-api.agent.localtest.me
   Keycloak      http://keycloak.agent.localtest.me (admin/admin, realm Dare-TRE)
   Adminer       http://adminer.agent.localtest.me
   Seq           http://seq.agent.localtest.me
   Camunda       http://camunda.agent.localtest.me

 ArgoCD          http://argocd.localtest.me (admin/admin)

 Dev login (both realms): dev/password123
 Vault keys: dev-env-setup/.vault-keys-$SUBMISSION_NS, .vault-keys-$AGENT_NS (gitignored)
===============================================================================
SUMMARY
