# dev-env-setup

One-command kind bootstrap for both product families (Submission and Agent) on a single
local cluster.

```
./cluster-setup.sh
```

Re-running it is safe: an existing `5s-tes` kind cluster is reused, and every step after
cluster creation - operator/ArgoCD installs included - is a `helm upgrade --install` or
`kubectl apply`, so it resumes correctly even if a previous run stopped right after
`kind create cluster`. A re-run does reset any `--set` toggle you added by hand to a
devstack install (e.g. `openldap.enabled`, `egress.enabled`) - re-apply those afterwards.
Only reach for `./clean-up.sh` (deletes the cluster and the saved
Vault keys) if the kind cluster itself looks broken rather than just mid-install.

## What it does

1. Creates the `5s-tes` kind cluster (`kind-config.yaml`), ports 80/443 mapped to the host
   (loopback-only, `listenAddress: "127.0.0.1"`).
2. Patches kind's `local-path-provisioner` so the `standard` StorageClass serves
   `ReadWriteMany` claims (single-node only - see the script's comment and the linked kind
   issue). This is why both product charts' RWX defaults
   (`submission.dataProtection.accessModes`, `agent.processModels.accessModes`) are left at
   `[ReadWriteMany]` locally instead of overridden to `ReadWriteOnce`.
3. Installs ingress-nginx.
4. Rewrites CoreDNS so `*.localtest.me` resolves in-cluster to the ingress controller
   (`files/deps/coredns.yaml`) - otherwise every pod's own loopback answers first, since
   `*.localtest.me` is a wildcard to `127.0.0.1`.
5. Installs cert-manager (+ self-signed `ClusterIssuer` `ca-issuer`), the CloudNativePG
   operator, the RabbitMQ Cluster Operator, and ArgoCD.
6. Creates both family namespaces, installs each family's devstack from the working tree
   (devstacks are never published; `--set global.ingress.host=<family>.localtest.me`),
   then applies
   `files/argo/submission-app.yaml`/`agent-app.yaml` — the two stack Applications (see
   **Stack Applications** below).
7. `vault-init.sh` initialises, unseals, and configures each family's own runtime Vault.
8. Waits for every ArgoCD Application, the CNPG `postgres` Cluster, and the `rabbitmq`
   RabbitmqCluster to be healthy, then for every Deployment in both namespaces.
9. Prints a URL summary.

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

## Stack Applications

`files/argo/submission-app.yaml` and `agent-app.yaml` deploy the published
`submission-stack`/`agent-stack` charts from `harbor.federated-analytics.ac.uk/5s-tes/chart`,
with every local override inline in each Application's `helm.valuesObject`. `targetRevision`
pins the chart version: a `0.0.0-pr.<number>` build while the branch's PR is open, the
released version after merge. `files/argo/repo.yaml` registers the OCI chart repos ArgoCD
pulls from.

The product apps (`submission.enabled`/`agent.enabled`) are `false` in both `valuesObject`s:
devs run the apps from the IDE against the in-cluster dependencies (root README, "Running
apps from VS Code against kind"). To run a product in-cluster instead, set `enabled: true`
with a published `chartVersion`/`imageVersion` in the same file and `kubectl apply` it.

The optional egress product follows the same pattern (`egress.enabled` in `agent-app.yaml`,
plus `--set egress.enabled=true` on the `agent-devstack` install) — see
`charts/agent-devstack/README.md` **Optional: local Data Egress**.

## Vault

Each family's own Vault (`vault.enabled` stays `true`, `vault.secretsEnabled=false` locally -
see both devstack READMEs' **Local install**) runs in the stack's own prod mode
(`server.standalone`, not `server.dev`), so it starts sealed on every fresh
cluster and needs real init/unseal.
`vault-init.sh <namespace> <kube-context> <vault-release-name>` (the third argument is
`submission-vault`/`agent-vault`):

```bash
./vault-init.sh 5s-tes-submission kind-5s-tes submission-vault
./vault-init.sh 5s-tes-agent kind-5s-tes agent-vault
```

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

## Host access for development

Each family's devstack chart (`charts/submission-devstack`, `charts/agent-devstack`,
`templates/dev-access.yaml`) renders `dev-*` NodePort Services for its stack's dependencies, so
a natively-running app (e.g. VS Code, not yet in-cluster) reaches every one of them at
`localhost:<port>` - see each devstack README's own "Host access for development" section for
the full port table. `kind-config.yaml`'s `extraPortMappings` map each nodePort straight through
to the same host port.

**Changing `kind-config.yaml` needs a cluster recreation** (`kind create cluster` reads it only
at cluster creation) - `./clean-up.sh && ./cluster-setup.sh`, not a plain re-run.

Per-app `appsettings.Development_Kind.json` profiles that consume these ports to run each app
from VS Code are documented in the root README's "Running apps from VS Code against kind".

## Known local constraints

- **GA4GH TES backend** (Agent): `agent.api.tesApiUrl` is left at the standalone chart's own
  dev default (a broken loopback address once in-cluster) per both devstack READMEs' own
  guidance - use `director-wfs.sh` for local TES testing, not `tesk.enabled`.
- **OpenLDAP** (Agent): off by default; the Credentials Camunda worker's LDAP bind fails
  until `openldap.enabled=true` is set on both the `agent-devstack` install and
  `agent-app.yaml`'s `valuesObject` (see `agent-devstack`'s README).
- **RWX and single-node only**: the provisioner patch in step 2 is documented as single-node
  only by kind itself; it is not a fix for a real multi-node RWX requirement.
- **Known platform limitation (Docker Desktop for Mac)**: despite `listenAddress: "127.0.0.1"`
  on every `extraPortMappings` entry, ports 80/443 remain reachable from the LAN - Docker
  Desktop forwards privileged host ports (<1024) through a path that doesn't honour the
  configured bind address (NodePorts ≥1024 are loopback-only). The
  exposure is the dev ingress, which sits in front of dev-grade credentials; to isolate it,
  firewall 80/443 or remap those two mappings to high ports (losing the plain `localtest.me`
  URLs).
