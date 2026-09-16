# submission-stack

Production stack for the Submission product: the `submission` standalone chart, its
dependencies, and the VaultSecrets that supply their passwords. No Deployment, Service or
Ingress for C# code lives in this chart; that is all in `charts/submission`.

## What this stack deploys

| Component | What it is | Sync wave |
|---|---|---|
| `templates/vault.yaml` | ArgoCD `Application` `vault`, hashicorp/vault chart, standalone (file) mode, with an unseal-watch sidecar | 1 |
| `templates/vault-init.yaml` | CronJob converging the runtime Vault (see **Vault** below) | 1 |
| `templates/secrets/*.yaml` | `VaultSecret` objects | 1 |
| `templates/rabbitmq.yaml` | `RabbitmqCluster` named `rabbitmq` | 2 |
| `templates/rustfs.yaml` | ArgoCD `Application` `rustfs`, RustFS chart, standalone mode | 3 |
| `templates/seq.yaml` | ArgoCD `Application` `seq`, Seq chart | 3 |
| `templates/postgres.yaml` | CNPG `Cluster` `postgres`, `Database` `dare-control`, `Pooler` `pg-pooler`, PodMonitors | 3 |
| `templates/backup.yaml` | Velero `Schedule` (volumes) + CNPG `ObjectStore`/`ScheduledBackup` (off by default) | 3 |
| `templates/submission.yaml` | ArgoCD `Application` `submission`, the standalone chart | 5 |

## What the cluster must already have

- **The CloudNativePG operator.** `templates/postgres.yaml` renders a `Cluster`, a
  `Database` and a `Pooler`; nothing runs unless the operator is watching for them.
  Requires CNPG **>= 1.25** for the `Database` CRD.
- **The RabbitMQ Cluster Operator**, for `templates/rabbitmq.yaml`'s `RabbitmqCluster`.
- **The redhatcop VaultSecret CRDs/operator**, for every object under `templates/secrets/`,
  with its default connection pointing at the platform Vault and a Kubernetes-auth role
  (`vault.role`) that accepts the `default` ServiceAccount in this namespace.
- **ArgoCD**, watching this namespace, with a project matching `global.argoProject`.
- **Velero**, in `global.veleroBackup.namespace`, if `global.veleroBackup.enabled` is `true`.
- A `monitoring.coreos.com` PodMonitor CRD (Prometheus Operator), if `global.monitoring.enabled`
  is `true`.

## Vault

Two Vault instances matter to this stack, with distinct jobs:

- **The platform Vault** supplies every deploy-time Secret. Each `VaultSecret` under
  `templates/secrets/` uses the redhatcop operator's default connection, authenticating
  with `vault.authPath`/`vault.role` (the tenant name) and reading the secret at
  `vault.secretPath` — see **What must be in Vault** below. Mounts, Kubernetes auth and
  policies there are platform-managed; this chart assumes they exist and never
  configures that Vault.
- **This stack's own runtime Vault** (`templates/vault.yaml`, release
  `submission-vault`) holds only the ephemeral researcher credentials the api reads and
  writes at runtime via `VaultCredentialsService` (`secret` kv-v2 mount,
  `api.vault.secretEngine`). File-storage standalone mode, explicitly not `server.dev`.

### The vault-init CronJob

`templates/vault-init.yaml` converges the runtime Vault every 5 minutes, so its whole
lifecycle is hands-off:

- initialises it on first run (1 key share) and stores the unseal key and root token in
  the `submission-vault-keys` Secret;
- ensures the `secret` kv-v2 mount and the `submission-app` policy (`secret/*`);
- mints a periodic app token, publishes it as the `submission-vault-token` Secret (key
  `vaultToken`, read by the api Deployment's `VaultSettings__Token`), and renews it on
  every later run so it never expires.

Unsealing is faster than the 5-minute cadence: an `unseal-watch` sidecar in the vault
pod (`server.extraContainers` in `templates/vault.yaml`) polls every 10 seconds and
unseals from the keys Secret — file-storage standalone always restarts sealed. The
CronJob also unseals, as a backstop.

Anyone who can read Secrets in this namespace can read the unseal key and root token —
acceptable for this Vault's ephemeral contents, and the reason it must never hold
anything else. If `submission-vault-token` is ever replaced (e.g. after a re-init), the
api Deployment's `secret.reloader.stakater.com/reload` annotation restarts it
automatically — on a cluster without Stakater Reloader (e.g. local kind) that restart
is manual. Locally, `dev-env-setup/vault-init.sh` only adds the fixed `dev-only-token`
for IDE-run apps on top of what the CronJob does.

The hashicorp/vault chart names its cluster-scoped `ClusterRoleBinding` from the
release name alone, so each stack's release is family-prefixed: `submission-vault` here,
`agent-vault` in `agent-stack`.

Renaming an existing installation's Vault release abandons its PVC and all sealed state —
a pre-existing install must migrate (re-attach the PVC under the new release name, or
re-init) before upgrading across this rename.

### What must be in Vault

One KV secret at `vault.secretPath` (default `kvv2/data/prod/prod/submission`):

| Key | Fills | Used for |
|---|---|---|
| `postgres_password` | `postgres-secret` / `password`, and the password inside `submission-api-secret` / `connectionString` | CNPG superuser. The connection string (`Server=pg-pooler;Port=5432;Database=<postgres.database>;User Id=postgres;Password=…`) is composed in `templates/secrets/submission.yaml`. |
| `kc_api_client_secret` | `submission-api-secret` / `keycloakClientSecret` | `Dare-Control-API` client secret |
| `kc_ui_client_secret` | `submission-ui-secret` / `keycloakClientSecret` | `Dare-Control-UI` client secret |
| `kc_admin_username`, `kc_admin_password` | `submission-api-secret` / `keycloakAdminUsername`, `keycloakAdminPassword` | The Keycloak admin user the api logs in as. See **Keycloak**. |
| `s3_access_key`, `s3_secret_key` | `submission-api-secret` / `s3AccessKey`, `s3SecretKey` and `submission-rustfs-secret` / `RUSTFS_ACCESS_KEY`, `RUSTFS_SECRET_KEY` | RustFS credentials; the api uses the same pair |
| `seq_admin_password` | `seq-admin-password-secret` / `password` | Seq's own first-run admin password (`firstRunAdminPasswordSecret`) |

A second KV secret at `vault.secretPath`**/rabbitmq** with keys `username` and `password`,
the names the RabbitMQ Cluster Operator's `secretBackend.vault` requires. The
`RabbitmqCluster` reads it directly; `submission-api-secret` reads the same two keys into
`rabbitUsername`/`rabbitPassword`.

The CNPG backup credentials are not under `vault.secretPath`: they come from
`postgres.backups.vault.path`, set only once backups are enabled. See **Backups**.

The standalone chart's README lists the Secret keys each component reads; the two must agree.

### RabbitMQ: the default user needs management permissions

`rabbitmq.vaultDefaultUser` (default `true`) gates only the `RabbitmqCluster`'s
`secretBackend.vault` block — production keeps Vault-backed credentials. Set it `false`
only on a cluster with no Vault to read from; the RabbitMQ Cluster Operator then
generates its own `rabbitmq-default-user` Secret with a random password instead.

`Shared/FiveSafesTes.Core/Rabbit/SetUpRabbitMQ.cs` connects with
`EasyNetQ.Management.Client`'s `ManagementClient(hostname, username, password)`, which
defaults to **port 15672 over plain HTTP** — the RabbitMQ **management HTTP API**, not
AMQP (5672). At startup the API creates a vhost, an exchange and a queue through that API.
The Vault-supplied default user at `vault.secretPath/rabbitmq` (read by both the
`RabbitmqCluster` and `submission-api-secret`) must therefore carry the `management` tag /
administrator permissions, not just messaging permissions, or vhost/exchange/queue setup
fails silently on every restart.

## Cookies

`templates/submission.yaml` wires `ui.sslCookies: "{{ .Values.global.ingress.tls }}"` — secure
cookies require HTTPS end-to-end, so this follows `global.ingress.tls` rather than being its own
stack value.

## Keycloak

The realm at `global.oidc.authority` must have:

- **Realm roles `dare-control-admin` and `dare-tre-admin`.** Both components authorise on
  realm roles (`[Authorize(Roles = ...)]`, read from the token's `realm_access.roles`). The
  api also looks `dare-tre-admin` up by name (`api.keycloakAdmin.serviceAccountRole`) and
  grants it to every TRE service account it creates.
- **Realm roles in the ID token.** The `roles` client scope's `realm roles` mapper must have
  *Add to ID token* on. Keycloak's stock mapper puts `realm_access.roles` in the access
  token only, and the ui builds its signed-in user from the ID token (userinfo claims other
  than the standard profile ones are dropped), so with the stock setting no ui role check
  passes.
- **`Dare-Control-UI`** — confidential client, standard flow. Valid redirect URI
  `https://submission.<global.ingress.host>/signin-oidc`; valid post-logout redirect URI
  `https://submission.<global.ingress.host>/signout-callback-oidc`. The ui forwards the
  signed-in user's access token to the api, which validates the audience, so this client's
  access tokens need an `aud` in `api.oidc.validAudiences` (default
  `Dare-Control-UI,Dare-Control-API,Dare-Control-Minio`): a client scope carrying an
  *Audience* mapper for `Dare-Control-API`, assigned to this client as a default scope.
- **`Dare-Control-API`** — confidential client; its client ID is the api's JWT audience.
  Direct Access Grants on: a TRE agent that has not yet uploaded its onboarding JSON, and the
  demo seed (`api.features.seedDemoData`), log in through this client with a password grant.
  Nothing uses this client's service account.
- **That audience scope in the realm's default client scopes.** The api creates a
  `tre-agent-<name>` client per TRE with no scope configuration, and the TRE's agent later
  calls the api with that client's client-credentials tokens. Those tokens carry an
  accepted `aud` only if the realm's *default client scopes* include the audience scope
  above.
- **The admin user** (`kc_admin_username`/`kc_admin_password`) — a user in this realm, not
  `master`; the api gets its token with a password grant through the realm's built-in
  `admin-cli` client. It needs the `realm-management` client roles `manage-clients`
  (create/delete a TRE client, read its secret), `view-realm` (read the `dare-tre-admin`
  role) and `manage-users` (map that role onto the client's service account).
- **`dare-control-admin` users need `realm-management` / `manage-users` too.** Adding or
  removing a project user makes the api write a `policy` attribute on that Keycloak user
  with the calling admin's own token.

Not needed by this stack: the `Dare-Control-Minio` client and `minio-authorization` scope
(they serve MinIO/RustFS OIDC login, which `templates/rustfs.yaml` does not configure), and
a `groups` claim (the ui declares group policies but no endpoint uses them).

`SubmissionKeyCloakSettings__Authority` renders as `<realm>/` (trailing slash, no well-known
suffix) and `__MetadataAddress` as `<realm>/.well-known/openid-configuration`, matching compose
— deliberate: `Submission.Api` sets `TokenValidationParameters.ValidateIssuer = false`
(`Submission.Api/Program.cs:117`), so `Authority`'s shape has no bearing on issuer validation.

## CloudNativePG: why a `Database` object, not `bootstrap.initdb.database`

CNPG's `bootstrap.initdb` is left at its defaults, which creates a database and a role
both named `app`. A declarative `Database` object, `dare-control`, then creates the real
application database, named `DARE-Control` (hyphenated, mixed case — intentional, for
compatibility with existing connection strings), owned by that same `app` role. This
needs the `Database` CRD, added in CloudNativePG 1.25.

## The shared `submission-dataprotection` PVC needs an RWX storage class

`submission.dataProtection.accessModes` defaults to `[ReadWriteMany]` (api and ui both mount
it, on a multi-node prod cluster), but is deployment-specific: a single-node kind cluster's
default provisioner is RWO-only, so local install overrides it to `[ReadWriteOnce]` (see
`submission-devstack`'s README). `global.storageClass` (`ceph-block`) is also typically
RWO-only. Set `submission.dataProtection.storageClassName` to the cluster's RWX-capable
class (e.g. its CephFS class) before deploying with the default `[ReadWriteMany]`, or the
PVC will not bind. Left `null` by default — the standalone chart then omits
`storageClassName` entirely and falls back to whatever the cluster's default class is,
which will fail for `ReadWriteMany` on a default class that is RWO-only.

## Backups

**Off by default for PostgreSQL** (`postgres.backups.enabled: false`): no destination S3
bucket has been set up for this stack yet. **On by default for volumes**
(`global.veleroBackup.enabled: true`), which the shared prod cluster's Velero picks up
by the `persistentVolumeLabels` selector.

Turning `postgres.backups.enabled` on also requires the `barman-cloud.cloudnative-pg.io`
CNPG plugin installed in the cluster; `templates/postgres.yaml`'s `Cluster.spec.plugins`
references it by name but does not install it.

With today's defaults, real data sits in four places with different protection:

- **`postgres` (the CNPG `Cluster`)** — its own data. Not backed up until
  `postgres.backups.enabled`, `destinationPath`, `endpointURL`, `endpointCASecretName`
  (a Secret with `ca.crt` for the destination's certificate — not created by this chart,
  must already exist) and `postgres.backups.vault.path`/`accessKeyField`/`secretKeyField`
  are all set. The S3 credentials themselves are **not** a separate Secret: they are a
  second `vaultSecretDefinitions` entry (aliased `backup`) on the same `postgres-secret`
  VaultSecret, reading `postgres.backups.vault.path` and filling `backupAccessKey`/
  `backupSecretKey` — the exact mechanism `serp-provisioning-stack` uses
  (`templates/secrets/postgres.yaml:19-27,33-36`, `templates/postgres.yaml:135-141`;
  same shape in `airlock-stack`'s `postgres.backups.vault.*` values), not a new one.
- **The `submission-dataprotection` PVC and RustFS's own storage** — ASP.NET
  data-protection keys and uploaded files. Both are labelled with
  `persistentVolumeLabels` (RustFS via its chart's `commonLabels` value, confirmed
  present and applied to its PVCs by `helm show values`/the chart's own
  `templates/pvc.yaml`), so both are covered by the Velero `Schedule` above.
- **Vault's own data volume** (`templates/vault.yaml`'s `server.dataStorage`) — **not** covered
  by the Velero `Schedule`: the vault chart's `server.dataStorage.labels` could carry the backup label but is
  not wired here.
- **The `seq` Application's own PVC** (`templates/seq.yaml`'s `persistence`) — **not** covered
  by the Velero `Schedule`, same reason: the datalust/seq chart has no equivalent label knob.

## Values reference

### Global

| Name | Description | Default |
|---|---|---|
| `global.argoProject` | ArgoCD project every `Application` uses. | `submission` |
| `global.namespace` | Namespace every object in this stack lives in. | `5s-tes-submission` |
| `global.oidc.authority` | Full Keycloak realm URL, passed to the standalone chart. | `https://keycloak.example.ac.uk/realms/Dare-Control` |
| `global.ingress.enabled` | Create Ingresses at all. | `true` |
| `global.ingress.host` | Base domain. `submission`/`submission-api` become subdomains of it. | `example.ac.uk` |
| `global.ingress.className` | Ingress controller class. | `nginx` |
| `global.ingress.certClusterIssuer` | cert-manager ClusterIssuer. | `ca-issuer` |
| `global.ingress.tls` | Terminate TLS at the ingress. | `true` |
| `global.storageClass` | Storage class for postgres, rabbitmq, rustfs, seq, vault. | `ceph-block` |
| `global.trustClusterCa.*` | Cluster CA bundle, passed to the standalone chart. | see values.yaml |
| `global.veleroBackup.enabled` | Create the Velero `Schedule`. | `true` |
| `global.veleroBackup.namespace` | Namespace the `Schedule` is created in. | `hiru-mgmt-velero` |
| `global.veleroBackup.schedule` | Five-field cron for the volume snapshot. | `0 1 * * *` |
| `global.veleroBackup.ttl` | How long Velero keeps each backup. | `168h0m0s` |
| `global.persistentVolumeLabels` | Labels passed to the standalone chart's PVC and the `Schedule`'s selector. | `{hiru.io/backup: "enabled"}` |
| `global.monitoring.enabled` | Push metrics to a Pushgateway; create PodMonitors. | `true` |
| `global.monitoring.pushgatewayUrl` | Pushgateway address. | see values.yaml |

### Vault

| Name | Description | Default |
|---|---|---|
| `vault.role` | Vault role the cluster's Kubernetes auth uses. | `submission` |
| `vault.secretPath` | KV path of the single secret holding every key in **What must be in Vault**. The RabbitMQ default user is a second secret at `<secretPath>/rabbitmq`. | `kvv2/data/prod/prod/submission` |
| `vault.authPath` | Kubernetes-auth mount. | `kubernetes` |
| `vault.address` | This stack's own runtime Vault Service address, passed to the api as `api.vaultUrl`. The VaultSecrets do not read from it. See **Vault** above. | `http://submission-vault:8200` |
| `vault.enabled` | Deploy this stack's own Vault `Application`. Runtime dependency (the API calls it directly for ephemeral credentials), so this stays `true` even where `vault.secretsEnabled` is `false`. | `true` |
| `vault.secretsEnabled` | Deploy every `VaultSecret` under `templates/secrets/`. `false` only where something else provides those Secrets (e.g. the devstack's static Secrets). | `true` |
| `vault.repoURL` | Helm repo the Vault chart is pulled from. | `https://helm.releases.hashicorp.com` |
| `vault.chart` | Chart name within that repo. | `vault` |
| `vault.chartVersion` | hashicorp/vault chart version. | `0.34.1` |
| `vault.dataStorageSize` | Vault's own data PVC size. | `10Gi` |
| `vault.injector.enabled` | Enable the Vault Agent Injector webhook. | `false` |

### submission (own app)

| Name | Description | Default |
|---|---|---|
| `submission.enabled` | Create the `submission` `Application`. | `true` |
| `submission.chartVersion` | Version of the `submission` chart in Harbor. Not published yet — first publish happens via the chart CI workflow; `1.0.0` is a placeholder pin. | `1.0.0` |
| `submission.imageVersion` | Image tag for both `submission-api` and `submission-ui`. | `3.2.0` |
| `submission.s3ConsoleUrl` | Public RustFS console URL. Empty computes one from `global.ingress`. | `""` |
| `submission.api.publicUrl` | Public API URL embedded in TRE onboarding JSON. Empty computes one from `global.ingress`. | `""` |
| `submission.dataProtection.storageClassName` | RWX-capable storage class for the shared `submission-dataprotection` PVC. `null` omits the field (cluster default, usually RWO-only). See above. | `null` |
| `submission.dataProtection.accessModes` | Access mode(s) for the shared `submission-dataprotection` PVC. Deployment-specific; see above. | `[ReadWriteMany]` |

### rustfs

| Name | Description | Default |
|---|---|---|
| `rustfs.enabled` | Deploy the `rustfs` `Application`. | `true` |
| `rustfs.repoURL` | Helm repo the RustFS chart is pulled from. | `https://rustfs.github.io/helm/` |
| `rustfs.chart` | Chart name within that repo. | `rustfs` |
| `rustfs.chartVersion` | RustFS chart version. | `1.0.0-rc.4` |
| `rustfs.storageSize` | Size of both the data and log PVCs. | `10Gi` |
| `rustfs.resources.requests.cpu` | CPU request. | `250m` |
| `rustfs.resources.requests.memory` | Memory request. | `512Mi` |
| `rustfs.resources.limits.memory` | Memory limit. | `512Mi` |

### seq

| Name | Description | Default |
|---|---|---|
| `seq.enabled` | Deploy the `seq` `Application`. | `true` |
| `seq.repoURL` | Helm repo the Seq chart is pulled from. | `https://helm.datalust.co` |
| `seq.chart` | Chart name within that repo. | `seq` |
| `seq.chartVersion` | Seq chart version. | `2025.2.1` |
| `seq.storageSize` | Size of Seq's data PVC. | `10Gi` |
| `seq.requireAuthForIngestion` | Require an API key for HTTP log ingestion (Seq's `firstRunRequireAuthenticationForHttpIngestion`, applied only on Seq's first run — changing it after Seq's data volume already has data has no effect). No `seqApiKey` is wired into either component's Secret, so leave `false` (mirrors airlock-stack) — `true` here rejects every app log. | `false` |
| `seq.resources.requests.cpu` | CPU request. | `250m` |
| `seq.resources.requests.memory` | Memory request. | `512Mi` |
| `seq.resources.limits.memory` | Memory limit. | `512Mi` |

### rabbitmq

| Name | Description | Default |
|---|---|---|
| `rabbitmq.replicas` | `RabbitmqCluster` replica count. | `1` |
| `rabbitmq.storageSize` | Size of the broker's data PVC. | `10Gi` |
| `rabbitmq.additionalConfig` | Extra `rabbitmq.conf` lines, passed to the operator verbatim. | `""` |
| `rabbitmq.vaultDefaultUser` | Default user credentials come from Vault, via the operator's own `secretBackend.vault`. `false` makes the operator generate its own `rabbitmq-default-user` Secret instead; only valid with `vault.secretsEnabled: false`, since `submission-api-secret` reads the same Vault path. See **RabbitMQ** above. | `true` |

### postgres

| Name | Description | Default |
|---|---|---|
| `postgres.database` | Name of the real application database (the `Database` object). See **CloudNativePG** above. | `DARE-Control` |
| `postgres.instances` | CNPG `Cluster` instance count. | `1` |
| `postgres.version` | PostgreSQL major/minor version. Changing this on a running cluster is a major upgrade. | `16.15` |
| `postgres.storageSize` | Size of the `Cluster`'s data PVC. | `10Gi` |
| `postgres.connectionPooler.instances` | `Pooler` (pgbouncer) instance count. | `1` |
| `postgres.connectionPooler.maxClientConn` | pgbouncer `max_client_conn`. | `3000` |
| `postgres.connectionPooler.defaultPoolSize` | pgbouncer `default_pool_size`. | `120` |
| `postgres.connectionPooler.reservePoolSize` | pgbouncer `reserve_pool_size`. | `20` |
| `postgres.connectionPooler.reservePoolTimeout` | pgbouncer `reserve_pool_timeout` (seconds). | `5` |
| `postgres.connectionPooler.serverIdleTimeout` | pgbouncer `server_idle_timeout` (seconds). | `300` |
| `postgres.backups.enabled` | Turn on CNPG's own `barman-cloud` backup (`ObjectStore`/`ScheduledBackup`). See **Backups** above. | `false` |
| `postgres.backups.destinationPath` | `s3://` path backups are written to. | `""` |
| `postgres.backups.endpointURL` | S3-compatible endpoint URL for the destination. | `""` |
| `postgres.backups.endpointCASecretName` | Secret with `ca.crt` for the destination's certificate. Not created by this chart. | `""` |
| `postgres.backups.retention` | How long CloudNativePG keeps backups in the bucket. | `30d` |
| `postgres.backups.schedule` | Six-field cron (seconds first) for the base backup. | `0 0 0 * * *` |
| `postgres.backups.vault.path` | Vault path holding the destination's S3 credentials. | `""` |
| `postgres.backups.vault.accessKeyField` | Field name at that path for the access key. | `access_key` |
| `postgres.backups.vault.secretKeyField` | Field name at that path for the secret key. | `secret_key` |
