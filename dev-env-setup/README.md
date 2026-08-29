# dev-env-setup

One-command kind bootstrap for both product families (Submission and Agent) on a single
local cluster. Cloned from `Director-Airlock/dev-env-setup` (RWX provisioner patch, CoreDNS
mechanism, idempotent re-run structure) with `SERP-Provisioning/dev-env-setup`'s operator
install steps as a secondary reference.

```
./cluster-setup.sh
```

Re-running it is safe: an existing `5s-tes` kind cluster is reused, and every `helm upgrade
--install` and `kubectl apply` is idempotent. If it stops partway through, just run it again
first; only reach for `./clean-up.sh` (deletes the cluster and the saved Vault keys) if the
kind cluster itself looks broken rather than just mid-install.

## What it does

1. Creates the `5s-tes` kind cluster (`kind-config.yaml`), ports 80/443 mapped to the host.
2. Patches kind's `local-path-provisioner` so the `standard` StorageClass serves
   `ReadWriteMany` claims (single-node only - see the script's comment and the linked kind
   issue). This is why both product charts' RWX defaults
   (`submission.dataProtection.accessModes`, `agent.processModels.accessModes`) are left at
   `[ReadWriteMany]` in the local values files instead of overridden to `ReadWriteOnce`.
3. Installs ingress-nginx.
4. Rewrites CoreDNS so `*.localtest.me` resolves in-cluster to the ingress controller
   (`files/deps/coredns.yaml`) - otherwise every pod's own loopback answers first, since
   `*.localtest.me` is a wildcard to `127.0.0.1`.
5. Installs cert-manager (+ self-signed `ClusterIssuer` `ca-issuer`), the CloudNativePG
   operator, the RabbitMQ Cluster Operator, and ArgoCD.
6. Builds the six app images (`submission-api`, `submission-ui`, `agent-api`, `agent-ui`,
   `agent-web`, `credentials-camunda`) from this working tree and loads them into kind -
   nothing is published to Harbor yet (see **Why the product charts are installed directly**).
7. Installs, in order: `submission-devstack` → `submission-stack` → `agent-devstack` →
   `agent-stack`, each from its local chart directory with its `files/values/*-local.yaml`.
8. `vault-init.sh` initialises, unseals, and configures each family's own runtime Vault.
9. Waits for every ArgoCD Application, the CNPG `postgres` Cluster, and the `rabbitmq`
   RabbitmqCluster to be healthy in both namespaces.
10. Installs `submission` and `agent` (the standalone product charts) directly, and waits for
    their Deployments.
11. Prints a URL summary.

## Why both families use a per-family ingress host suffix, not `localtest.me` directly

Each devstack README's own "Local install" recipe uses `global.ingress.host=localtest.me`,
written for running **one family at a time**. Running both on the same cluster (what this
bootstrap does) exposed a real collision: `submission-devstack`/`agent-devstack` both render a
Keycloak Ingress at `keycloak.{{ global.ingress.host }}`, and both render Adminer at
`adminer.{{ global.ingress.host }}`; `submission-stack`/`agent-stack` both render Seq at
`seq.{{ global.ingress.host }}` and RustFS at `rustfs.{{ global.ingress.host }}`. With one
shared host, ingress-nginx keeps only the older of the two same-host Ingresses per host and
silently drops the other - so one family's Keycloak/Adminer/Seq/RustFS becomes unreachable.

Fix used here: give each family its own suffix, both still under the `*.localtest.me`
wildcard (which resolves any subdomain, at any depth, to `127.0.0.1`):

- Submission: `global.ingress.host=submission.localtest.me`
- Agent: `global.ingress.host=agent.localtest.me`

No chart changes were needed - `global.ingress.host` was already the values field meant for
exactly this. Running only one family locally still works with the plain `localtest.me` host
from the README recipes; the split host is only needed once both run together, which is what
this bootstrap does by default. Both devstack READMEs' "Local install" sections now note this.

## Why the product charts are installed directly, not through ArgoCD

`submission-stack`/`agent-stack`'s own `templates/submission.yaml`/`agent.yaml` render an
ArgoCD `Application` pointing at `harbor.federated-analytics.ac.uk/5s-tes/chart` (a literal in
the template, not a value, per this repo's convention that only `targetRevision` is
surfaced as a value for the org's own chart releases) with `chart: submission`/`agent`. No
version has ever been published there (Task 4.1 built the publish workflow; it has not been
run), and the two clone sources' pattern of pulling PR-tagged builds from a real Harbor
doesn't apply here - there's nothing to pull yet.

Rather than stand up a throwaway OCI registry and redirect that hardcoded hostname to it
(more moving parts, and still not "installed from the local paths" per the task brief), this
bootstrap sets `submission.enabled: false`/`agent.enabled: false` in the *-stack-local.yaml
values (a local-only override of an existing, genuinely deployment-specific toggle - never a
chart default change) so that inner `Application` never renders, and instead directly runs
`helm upgrade --install submission ../charts/submission -f files/values/submission-product-local.yaml`
(and the same for `agent`). `files/values/*-product-local.yaml` reproduce exactly the
`helm.valuesObject` the disabled `Application` would have rendered, for this bootstrap's
local settings - kept in sync by hand against `templates/submission.yaml`/`agent.yaml` since
there are only two of them.

This is a bootstrap-script delivery-mechanism decision, not a chart change: the full stack
still runs by default locally (Decision 7 - a developer turns a component off from the
README when running it from VS Code, Task 5.3), just orchestrated by plain `helm install`
against the working tree instead of ArgoCD-via-Harbor. Once a first `submission`/`agent`
chart release lands in Harbor, `submission.enabled`/`agent.enabled` can flip back to `true`
and this bootstrap can drop its two `*-product-local.yaml` files and direct `helm install`
steps.

### Local images

Nothing is published to Harbor for the five C# components or `agent-web` either. The script
builds all six from this working tree (`docker build`, native platform - Apple Silicon builds
arm64) and `kind load docker-image`s them, tagged `5s-tes/<component>:local`; the product
values files point each component's `image.repository`/`image.tag` at these instead of the
chart's own Harbor default, with `pullPolicy: IfNotPresent` (already the chart default) so
nothing tries to reach Harbor. Rebuilding is cheap on a re-run (Docker layer cache); a code
change needs `./cluster-setup.sh` run again to rebuild and reload.

**Known limitation, not fixed here**: `Submission.Api/Dockerfile` and
`Credentials.Camunda/Dockerfile` `wget` a hardcoded `linux-amd64` `mc` (MinIO client) binary
into their final image regardless of target platform. Built natively on Apple Silicon, the
resulting image is `arm64` with an `amd64` `mc` binary that cannot execute. This only affects
code paths that shell out to `mc` (S3 user/policy provisioning); it does not stop the pods
from starting. Fixing the Dockerfile (an arch-aware download, or a multi-arch `mc` release)
is outside this task's scope - flagged here for whoever picks up S3 provisioning testing.

## Vault

Each family's own Vault (`vault.enabled` stays `true`, `vault.secretsEnabled=false` locally -
see both devstack READMEs' **Local install**) runs in the stack's own prod mode
(`server.standalone`, not `server.dev` - Decision 5), so it starts sealed on every fresh
cluster and needs real init/unseal. `vault-init.sh <namespace> <kube-context>`:

1. Initialises with `-key-shares=1 -key-threshold=1` (single operator, local dev only) if not
   already initialised, saving the unseal key and root token to
   `dev-env-setup/.vault-keys-<namespace>` (gitignored, `chmod 600`).
2. Unseals if sealed, using the saved key.
3. Enables the `secret` kv-v2 mount at runtime (`api.vault.secretEngine` default) - the
   *only* mount needed locally, since `vault.secretsEnabled=false` means no VaultSecret CRs
   exist to need the operator-read `kvv2` mount or Kubernetes auth.
4. Creates a token with the literal ID `dev-only-token` and the root policy, as a child of
   the real (random) root token init produced - `vault operator init` cannot choose the root
   token's own ID, so this is how the static Secrets' `vaultToken: dev-only-token` becomes a
   real, working token without patching any Secret or restarting any pod.

Delete `dev-env-setup/.vault-keys-<namespace>` only together with that Vault's PVC (e.g. via
`./clean-up.sh`) - an orphaned keys file for an already-initialised Vault cannot unseal it.

## Known local constraints

- **GA4GH TES backend** (Agent): `agent.api.tesApiUrl` is left at the standalone chart's own
  dev default (a broken loopback address once in-cluster) per both devstack READMEs' own
  guidance - use `director-wfs.sh` for local TES testing, not `tesk.enabled`.
- **OpenLDAP** (Agent): off by default; the Credentials Camunda worker's LDAP bind fails
  until `openldap.enabled=true` is set on both `agent-devstack-local.yaml` and
  `agent-stack-local.yaml` (see `agent-devstack`'s README).
- **RWX and single-node only**: the provisioner patch in step 2 is documented as single-node
  only by kind itself; it is not a fix for a real multi-node RWX requirement.
