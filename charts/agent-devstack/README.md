# agent-devstack

Dev-only extras for running the Agent stack on a local cluster.
**Never deployed to a shared cluster, never referenced by a tenant deploy repo.**

Install order locally: bootstrap script → this chart → the
[`agent-stack`](../agent-stack/README.md) chart.

## What it provides, and why

Only things production provides another way (doc 09):

| Component | Production provides it as | Here |
|---|---|---|
| Identity provider | Hosted Keycloak `Dare-TRE` realm | Bitnami Keycloak `Application` with a hand-written dev realm import and its own bundled dev PostgreSQL |
| App and dependency secrets | `agent-stack`'s `VaultSecret`s | Static `Secret`s under the same names (`postgres-secret`, `agent-api-secret`, `agent-ui-secret`, `agent-web-secret`, `credentials-camunda-secret`, `agent-rustfs-secret`, `seq-admin-password-secret`, `agent-openldap-secret` if `openldap.enabled`) |
| Database GUI | — (ops tooling) | Adminer `Application` |
| External TRE data database | Whatever real database `credentials-camunda-secret.connectionStringTreData` is pointed at | Bitnami PostgreSQL `Application` named `tredata`, a disposable stand-in |

No mailpit, no Camunda/RabbitMQ/CNPG substitute: `agent-stack` deploys its own Camunda, its
own `RabbitmqCluster` and its own CNPG `Cluster` in both environments (doc 09 — the stack chart
is identical everywhere, operators come from the bootstrap script).

## Credentials

All local credentials are **fixed, simple strings**, committed on purpose so a recreated
kind cluster keeps the same passwords and nothing in a developer's local configuration has
to change. This is an approved exception to the no-committed-secrets rule (doc 09, rule 1),
and applies only to this chart.

**Each credential is defined exactly once and reused** (doc 09, rule 2): every client
secret in the realm import string-matches the value in `templates/secrets/static.yaml`.

| Credential | Value | Where it's set | Where it's read |
|---|---|---|---|
| PostgreSQL superuser | `postgres` / `password123` | `postgres-secret` | `agent-stack`'s CNPG `Cluster` superuserSecret; also embedded in `agent-api-secret`'s/`credentials-camunda-secret`'s connection strings |
| `Dare-TRE-API` client secret | `devsecret-tre-api` | realm import only | Not directly consumed by any app Secret — `Dare-TRE-API` is a valid audience for `Dare-TRE-UI` tokens (`api.oidc.validAudiences`), not a client the chart authenticates as. `serviceAccountsEnabled` stays on to mirror the prod realm's client shape, but has no realm role grant: no verified consumer of a client-credentials token exists in this codebase |
| `Dare-TRE-UI` client secret | `devsecret-tre-ui` | realm import + `agent-api-secret.treKeycloakClientSecret`, `agent-ui-secret.keycloakClientSecret`, `agent-web-secret.keycloakClientSecret` | `TREKeyCloakSettings__ClientSecret` (api), `KEYCLOAK_CLIENT_SECRET` (ui, web) — the single client used by api, ui and web |
| `Dare-TRE-S3` client secret | `devsecret-tre-s3` | realm import only | Not yet consumed by any app Secret — the client exists for a future S3-console SSO wire-up, same status as `submission-devstack`'s `Dare-Control-S3` |
| Dev login user | `dev` / `password123` | realm import only | Manual browser login; holds the `dare-tre-admin` realm role so agent-web's role-gated pages work (`authcheck("dare-tre-admin")`, see **agent-web role gating** below) |
| `Dare-Control-API` client secret (cross-realm) | `devsecret-control-api` | `agent-api-secret.submissionKeycloakClientSecret` only — not in this chart's own realm | `SubmissionKeyCloakSettings__ClientSecret` equivalent on the api side. Must equal `submission-devstack`'s own `Dare-Control-API` secret; only matters if the Submission product is also running locally (cross-realm token validation) |
| `Data-Egress-API` client secret | `dev-egress-unused` | `agent-api-secret.egressKeycloakClientSecret` | Only read when `api.egress.enabled` is `true` (not surfaced by `agent-stack`, default `false`) |
| S3 (RustFS) access/secret key | `s3-tre` / `s3-tre-pass` | `agent-api-secret.s3AccessKey`/`s3SecretKey` + `agent-rustfs-secret.RUSTFS_ACCESS_KEY`/`RUSTFS_SECRET_KEY` | RustFS chart's own root credentials; api's S3 client |
| Vault token | `dev-only-token` | `agent-api-secret.vaultToken` + `credentials-camunda-secret.vaultToken` | `VaultSettings__Token` on both api and the Credentials Camunda worker — the local Vault runs (`vault.enabled` stays `true`; see **Local install**) and must be configured with a token equal to this value after init/unseal, or these Secrets' values updated to match the real token |
| RabbitMQ default user | `agent` / `password123` | `agent-api-secret.rabbitUsername`/`rabbitPassword` | `RabbitMQ__Username`/`Password` — must match the stack's `rabbitmq.additionalConfig` (see **Local install**) |
| Better Auth secret | `dev-agent-betterauth-secret-fixed-32c` | `agent-web-secret.betterAuthSecret` | `BETTER_AUTH_SECRET` |
| Encryption key | `ZGV2LWFnZW50LWVuY3J5cHRpb24ta2V5LTMyYnl0ZSE=` (base64, 32 bytes) | `agent-api-secret.encryptionKey` | The api's encryption key setting |
| Hangfire dashboard | `admin` / `password123` | `agent-api-secret.hangfireUsername`/`hangfirePassword` | Hangfire basic auth |
| Hasura admin secret | `dev-hasura-admin-secret-unused` | `agent-api-secret.hasuraAdminSecret` | Only read when `api.hasura.enabled` is `true` (not surfaced by `agent-stack`, default `false`) |
| LDAP admin bind password | `admin` | `credentials-camunda-secret.ldapAdminPassword` + `agent-openldap-secret.LDAP_ADMIN_PASSWORD`/`LDAP_CONFIG_ADMIN_PASSWORD` (if `openldap.enabled`) | Credentials Camunda worker's directory bind; OpenLDAP's own admin/config-admin passwords. Matches the stack's `openldap.yaml` chart contract |
| tredata (TRE data DB stand-in) | `postgres` / `password123`, database `tredata` | `tredata.yaml`'s own `auth.postgresPassword`/`auth.database` | `credentials-camunda-secret.connectionStringTreData`, pointed at the `tredata-postgresql` Service (Bitnami chart's own naming, release name `tredata`) |
| Keycloak admin console | `admin` / `admin` | `keycloak-admin-secret` | Keycloak's own `auth.existingSecret` |
| Seq first-run admin | `admin` / `admin` | `seq-admin-password-secret` | Seq's own `firstRunAdminPasswordSecret` |
| Adminer admin | `admin` / `admin` | `adminer-admin-password` | Adminer's own `auth.existingSecret` |

### agent-web role gating

`Agent/agent-web/lib/auth-helpers.ts:24-47` (`authcheck`) redirects to `/forbidden` unless the
signed-in user's realm roles (read from the OIDC token's `realm_access.roles`, mapped in
`Agent/agent-web/lib/auth.ts:39-43`) include the required role. Every gated page in
`Agent/agent-web/app/` calls `authcheck("dare-tre-admin")`
(`app/page.tsx:6`, `app/access-rules/page.tsx:20`, `app/projects/page.tsx:25`,
`app/projects/[projectId]/page.tsx:21`, `app/configure-5s-tes/page.tsx:14`) — so the `dev` user
holds `dare-tre-admin` in the realm import above.

## Local install

Install this chart first, then `agent-stack` configured to hand off to its static Secrets,
drop the objects a local cluster doesn't have, and reach the local Keycloak, e.g. (mirrors
`serp-provisioning`'s `dev-env-setup/files/argo/app.yaml`):

```
--set global.oidc.authority=http://keycloak.localtest.me/realms/Dare-TRE
--set global.ingress.host=localtest.me
--set global.ingress.tls=false
--set global.storageClass=standard
--set global.trustClusterCa.enabled=false
--set global.veleroBackup.enabled=false
--set global.monitoring.enabled=false
--set vault.secretsEnabled=false
--set rabbitmq.vaultDefaultUser=false
--set-string rabbitmq.additionalConfig="default_user = agent
default_pass = password123
loopback_users.agent = false"
--set 'agent.processModels.accessModes[0]=ReadWriteOnce'
```

A future `dev-env-setup/` values file is planned to carry this set; until it exists, this
recipe is canonical.

- `global.oidc.authority` must point at the local Keycloak's realm URL; the stack chart's
  own default is the production authority, which does not exist locally.
- `global.ingress.host=localtest.me` matches this chart's own `global.ingress.host`, so
  `agent`/`agent-api`/`seq`/`rustfs`/`camunda` land on the same local DNS suffix as
  `keycloak`/`adminer`.
- `global.ingress.tls=false`: no cert-manager `ClusterIssuer` exists locally by default.
- `global.storageClass=standard`: kind's built-in default `StorageClass` (via
  `local-path-provisioner`), replacing the stack's Ceph-specific default.
- `global.trustClusterCa.enabled=false`: no `overlay-castore` ConfigMap exists locally;
  with it `false`, the standalone chart mounts no certs-overlay volume.
- `global.veleroBackup.enabled=false`: no Velero runs locally; with it `false`,
  `templates/backup.yaml` renders no `Schedule`.
- `global.monitoring.enabled=false`: no Prometheus Operator runs locally; with it `false`,
  `templates/postgres.yaml` renders no `PodMonitor`s.
- `vault.enabled` stays `true`: the stack's own Vault `Application` still deploys — it's a
  runtime dependency, api and the Credentials Camunda worker call it directly for ephemeral
  credentials, not just a source for bootstrap Secrets. `vault.secretsEnabled=false` drops only
  the `VaultSecret`s under `templates/secrets/`, so this chart's static Secrets are the only
  thing producing those names/keys.
- The local Vault still starts sealed and needs init/unseal (a helper script is planned;
  see `agent-stack`'s README **Vault** section for the manual steps). Its runtime token is
  `agent-api-secret.vaultToken`/`credentials-camunda-secret.vaultToken` (`dev-only-token` — see
  **Credentials** above): either configure the freshly-initialized local Vault with a token
  equal to that value, or update this chart's `templates/secrets/static.yaml` to match whatever
  token init actually produced.
- `rabbitmq.vaultDefaultUser=false` drops the `RabbitmqCluster`'s `secretBackend.vault`
  block. The `additionalConfig` lines then set the broker's own default user to
  `agent`/`password123` — the same values as this chart's `agent-api-secret`
  (`rabbitUsername`/`rabbitPassword` above), so the app's RabbitMQ credential is defined
  once and reused into the broker, not redefined.
- `agent.processModels.accessModes[0]=ReadWriteOnce`: kind's default provisioner
  (`local-path-provisioner`) only binds `ReadWriteOnce` claims; the stack's own default
  (`[ReadWriteMany]`) never binds locally.

### Optional: local OpenLDAP

`agent-stack`'s Credentials Camunda worker binds to a directory (`agent.ldap.*`), production's
external AD. To stand up the bundled OpenLDAP instead:

```
--set openldap.enabled=true    # on THIS chart, so agent-openldap-secret renders
```

and, on `agent-stack`, `--set openldap.enabled=true` (deploys the actual `openldap`
Application). `agent.ldap.*`'s own defaults already match the OpenLDAP chart's defaults
(`host: openldap`, `port: 389`, `baseDn: dc=camundaephemeral,dc=local`) — leave them alone.
Both toggles use `admin` for the bind/config-admin passwords (see **Credentials** above).

### GA4GH TES backend

`agent-stack`'s own dev default for `agent.api.tesApiUrl` (`http://localhost:8000/v1/tasks`) is
a broken loopback address once actually in-cluster. **For local TES testing, use
`director-wfs.sh`** (`/Users/alex/Devel/feda/director-wfs`) to bring up a disposable kind-based
TESK environment, then set `agent.api.tesApiUrl` on `agent-stack` to its `tesk-api` Service —
do not enable `agent-stack`'s own `tesk.enabled` for this purpose (see its README, **GA4GH TES
backend**). A local GA4GH Funnel instance is an equally valid substitute if you have one.

### Known gap: the shared processmodels PVC needs ReadWriteMany

`agent-stack`'s `templates/agent.yaml` hard-codes `processModels.accessModes: [ReadWriteMany]`
(api and camunda share the PVC) regardless of environment. kind's default `standard`
`StorageClass` (`local-path-provisioner`) is ReadWriteOnce-only, so this PVC will not bind on
an unmodified local cluster. Either install an RWX-capable provisioner (e.g. an NFS
provisioner) in the bootstrap script and set `agent.processModels.storageClassName` to it, or
run api and camunda pinned to the same node so a ReadWriteOnce claim happens to work — neither
is wired up by this devstack chart yet.

## What the local cluster must already have

With the override set above, Velero and a Prometheus Operator `PodMonitor` CRD are
**not** required locally (nothing renders that needs them). The setup script
(`dev-env-setup/`, not yet created in this repo) is expected to install, before both
charts:

- ingress-nginx,
- ArgoCD, watching `Application`s in `5s-tes-agent`, with a matching `AppProject`,
- the CloudNativePG operator,
- the RabbitMQ Cluster Operator.

## Local Keycloak

- URL: `http://keycloak.localtest.me` (admin console), realm `Dare-TRE`. Plain HTTP:
  `templates/keycloak.yaml`'s `ingress` block sets no `tls` key, and the chart's default
  is `false`.
- `agent-stack`'s `global.oidc.authority` must be overridden to reach this Keycloak
  — see **Local install** above. The standalone chart derives web's
  `NEXT_PUBLIC_KEYCLOAK_URL`/`NEXT_PUBLIC_KEYCLOAK_REALM` from this authority via `urlParse`
  (`charts/agent/templates/web/deployment.yaml:2,84-86`), so the realm's last path segment
  must be exactly `Dare-TRE`.
- The dev realm mirrors the external prod realm's shape (same realm name `Dare-TRE`, same
  three client IDs) but is a hand-written, minimal stand-in: no protocol mappers, no audience
  scopes, `sslRequired: none`, and pure wildcard `redirectUris`/`webOrigins` (`["*"]`) — dev
  shortcuts, never to be copied into a real realm.
- **Known local constraint**: server-side OIDC calls made from inside pods (api, ui and web
  reaching `global.oidc.authority`) resolve `keycloak.localtest.me` to `127.0.0.1`, not the
  ingress controller — `localtest.me` is a wildcard domain that always resolves to
  loopback. This needs a cluster DNS mapping to the ingress controller; a CoreDNS rewrite
  is planned for the dev-env bootstrap but not yet implemented. Until then, map it manually
  in cluster DNS (or `/etc/hosts` on every node) before the login flow will work.

## Values

| Value | Description | Default |
|---|---|---|
| `global.namespace` / `global.argoProject` | Must match `agent-stack` | `5s-tes-agent` / `agent` |
| `global.ingress.host` / `className` | Local DNS suffix and ingress class | `localtest.me` / `nginx` |
| `keycloak.*` | Bitnami chart pin + org image mirror | `25.4.0` / `harbor.ukserp.ac.uk` |
| `adminer.*` | Chart pin | `0.1.8` |
| `tredata.*` | Bitnami PostgreSQL chart pin + org image mirror, dev stand-in for the external TRE data database | `18.8.13` / `harbor.ukserp.ac.uk` |
| `openldap.enabled` | Render `agent-openldap-secret`. Set alongside `agent-stack`'s own `openldap.enabled` — see **Optional: local OpenLDAP** | `false` |

## Limits

- The dev realm is a minimal starting point: three clients, two users (including the
  `Dare-TRE-API` service account), one realm role. No protocol mappers or client scopes.
  Extend the realm by editing `templates/keycloak-realm.yaml`.
- Keycloak only imports a realm on first start; it skips an existing one. To apply a
  `templates/keycloak-realm.yaml` change to an already-running local Keycloak, delete the
  `keycloak` Application's PostgreSQL PVC (or delete the realm via the admin console) and
  let ArgoCD re-sync.
- This chart is installed from the working tree; it is not published to Harbor and has no
  release workflow.
