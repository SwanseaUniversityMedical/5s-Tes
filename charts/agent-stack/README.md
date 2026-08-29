# agent-stack

Production stack for the Agent product: the `agent` standalone chart, its dependencies, and
the VaultSecrets that supply their passwords. No Deployment, Service or Ingress for C# code
lives in this chart; that is all in `charts/agent`.

## What this stack deploys

| Component | What it is | Sync wave |
|---|---|---|
| `templates/vault.yaml` | ArgoCD `Application` `vault`, hashicorp/vault chart, standalone (file) mode | 1 |
| `templates/secrets/*.yaml` | `VaultSecret` objects | 1 |
| `templates/rabbitmq.yaml` | `RabbitmqCluster` named `rabbitmq` | 2 |
| `templates/openldap.yaml` | ArgoCD `Application` `openldap`, optional, default off | 2 |
| `templates/rustfs.yaml` | ArgoCD `Application` `rustfs`, RustFS chart, standalone mode — the TRE object store | 3 |
| `templates/seq.yaml` | ArgoCD `Application` `seq`, Seq chart | 3 |
| `templates/camunda.yaml` | ArgoCD `Application` `camunda`, camunda-platform chart, Zeebe only | 3 |
| `templates/tesk.yaml` | ArgoCD `Application` `tesk`, optional, default off | 3 |
| `templates/postgres.yaml` | CNPG `Cluster` `postgres`, `Database`s `dare-tre`/`tre-credentials`, `Pooler` `pg-pooler`, PodMonitors | 3 |
| `templates/backup.yaml` | Velero `Schedule` (volumes) + CNPG `ObjectStore`/`ScheduledBackup` (off by default) | 2/3 |
| `templates/agent.yaml` | ArgoCD `Application` `agent`, the standalone chart | 5 |

## What the cluster must already have

- **The CloudNativePG operator**, `>= 1.25` for the `Database` CRD. `templates/postgres.yaml`
  renders a `Cluster`, two `Database` objects and a `Pooler`; nothing runs unless the operator
  is watching for them.
- **The RabbitMQ Cluster Operator**, for `templates/rabbitmq.yaml`'s `RabbitmqCluster`.
- **The redhatcop `vault-config-operator`** (the `VaultSecret` CRD), for every object under
  `templates/secrets/`.
- **ArgoCD**, watching this namespace, with a project matching `global.argoProject`.
- **Velero**, in `global.veleroBackup.namespace`, if `global.veleroBackup.enabled` is `true`.
- **The `barman-cloud.cloudnative-pg.io` CNPG plugin**, only if `postgres.backups.enabled` is
  turned on — see **Backups** below.
- A `monitoring.coreos.com` PodMonitor CRD (Prometheus Operator), if `global.monitoring.enabled`
  is `true`.

## Vault

This stack deploys its own Vault instance (`templates/vault.yaml`): an **app-owned runtime
Vault**, holding ephemeral researcher credentials that both `api` and the Credentials Camunda
worker call at runtime via `VaultSettings__BaseUrl`/`VaultSettings__Token` — not the platform
Vault on the `management` cluster. This mirrors `submission-stack`'s Vault, for the same reason
(Alex, 2026-08-29).

Because it is not the platform Vault, every `VaultSecret` below sets
`vaultSecretDefinitions[].connection.address` to `vault.address` (default `http://vault:8200`,
this stack's own Vault Service) to override the redhatcop operator's own default connection,
which otherwise points at the platform Vault.

It starts sealed, using file storage (`server.standalone`, explicitly not `server.dev`). Every
step below is manual; `dev-env-setup/vault-init.sh` automates the local-dev equivalent
(init/unseal/`secret` mount/token).

**Known hazard, on a cluster also running `submission-stack`'s Vault**: the two releases'
`ClusterRoleBinding`s collide (identical cluster-scoped name, one release's naming choice
away from a real fix) — see `submission-stack`'s README **Vault** section for the detail.
Vault itself keeps working; only ArgoCD's sync status for one of the two is affected.

1. **Init and unseal** (first time only):

   ```bash
   kubectl exec -n <namespace> -it vault-0 -- vault operator init
   # Record the five unseal keys and the root token somewhere safe (not Git).
   kubectl exec -n <namespace> -it vault-0 -- vault operator unseal   # x3, different keys
   ```

2. **Enable the kv-v2 mount** at the path base — the first segment of `vault.secretPath`
   (`kvv2` by default):

   ```bash
   kubectl exec -n <namespace> -it vault-0 -- vault login   # root token from step 1
   kubectl exec -n <namespace> -it vault-0 -- vault secrets enable -path=kvv2 kv-v2
   ```

   Also enable a second mount, `secret` (KV v2 — `VaultCredentialsService` reads/writes
   `v1/{mount}/data/{path}`, the KV v2 shape: `Shared/FiveSafesTes.Core/Services/VaultCredentialsService.cs:38,60,82,117`),
   at `api.vault.secretEngine`/`VaultSettings__SecretEngine`'s default (`secret`, compose
   parity):

   ```bash
   kubectl exec -n <namespace> -it vault-0 -- vault secrets enable -path=secret -version=2 kv
   ```

   `kvv2` and `secret` are two different mounts for two different things: `kvv2` is the
   operator-read store the redhatcop `VaultSecret`s pull deploy-time app Secrets from;
   `secret` is the store `api`/`camunda` read and write at runtime (ephemeral researcher
   credentials, via `VaultCredentialsService`).

3. **Enable Kubernetes auth** at `vault.authPath`, and point it at this cluster's API:

   ```bash
   kubectl exec -n <namespace> -it vault-0 -- vault auth enable -path=kubernetes kubernetes
   kubectl exec -n <namespace> -it vault-0 -- vault write auth/kubernetes/config \
     kubernetes_host="https://kubernetes.default.svc"
   ```

4. **Create the policy and role** (`vault.role`), bound to the namespace's `default`
   ServiceAccount — every VaultSecret's `authentication.serviceAccount.name` is `default`:

   ```bash
   kubectl exec -n <namespace> -it vault-0 -- vault policy write agent - <<'EOF'
   path "kvv2/data/prod/prod/agent/*" {
     capabilities = ["read"]
   }
   EOF
   kubectl exec -n <namespace> -it vault-0 -- vault write auth/kubernetes/role/agent \
     bound_service_account_names=default \
     bound_service_account_namespaces=<namespace> \
     policies=agent \
     ttl=1h
   ```

   Adjust the policy path to match `vault.secretPath` if it is overridden.

5. **Write the app secrets**, one per row of the paths table below. The `vault kv put` CLI
   inserts the kv-v2 `data/` segment itself, so drop it from the path you type:

   ```bash
   kubectl exec -n <namespace> -it vault-0 -- vault kv put kvv2/prod/prod/agent/postgres \
     postgres_password='...'
   ```

   Last, write the api's and the Camunda worker's own Vault tokens, at
   `{{ .Values.vault.secretPath }}/agent-api` and `.../credentials-camunda`, key `vault_token`
   both times, so `agent-api-secret` and `credentials-camunda-secret` can inject them as
   `vaultToken` (`VaultSettings__Token`).

### Vault paths (under `vault.secretPath`, default `kvv2/data/prod/prod/agent`)

The standalone chart's README lists the Kubernetes Secret names and keys each of these fills;
the two must agree.

| Path | Field | Fills | Used for |
|---|---|---|---|
| `.../postgres` | `postgres_password` | `postgres-secret` / `password` | CNPG superuser password |
| `.../agent-api` | `connection_string_default` | `agent-api-secret` / `connectionStringDefault` | PostgreSQL connection string for `DARE-Tre` on `pg-pooler` |
| `.../agent-api` | `connection_string_credentials` | `agent-api-secret` / `connectionStringCredentials` | PostgreSQL connection string for `TRE_Credentials` on `pg-pooler` |
| `.../agent-api` | `tre_keycloak_client_secret` | `agent-api-secret` / `treKeycloakClientSecret` | `Dare-TRE-UI` client secret (api authenticates as this client) |
| `.../agent-api` | `submission_keycloak_client_secret` | `agent-api-secret` / `submissionKeycloakClientSecret` | `Dare-Control-API` client secret (cross-realm; see below) |
| `.../agent-api` | `egress_keycloak_client_secret` | `agent-api-secret` / `egressKeycloakClientSecret` | `Data-Egress-API` client secret. Only read when `api.egress.enabled` is `true` (not surfaced by this stack) |
| `.../agent-api` | `s3_access_key` | `agent-api-secret` / `s3AccessKey` | Agent RustFS access key. Must equal `.../rustfs`'s `access_key` |
| `.../agent-api` | `s3_secret_key` | `agent-api-secret` / `s3SecretKey` | Agent RustFS secret key. Must equal `.../rustfs`'s `secret_key` |
| `.../agent-api` | `rabbit_username` | `agent-api-secret` / `rabbitUsername` | RabbitMQ default user (see below) |
| `.../agent-api` | `rabbit_password` | `agent-api-secret` / `rabbitPassword` | RabbitMQ default user password |
| `.../agent-api` | `encryption_key` | `agent-api-secret` / `encryptionKey` | Base64 encryption key |
| `.../agent-api` | `vault_token` | `agent-api-secret` / `vaultToken` | Token for this stack's own Vault, used by the api |
| `.../agent-api` | `hangfire_username` | `agent-api-secret` / `hangfireUsername` | Hangfire dashboard username |
| `.../agent-api` | `hangfire_password` | `agent-api-secret` / `hangfirePassword` | Hangfire dashboard password |
| `.../agent-api` | `hasura_admin_secret` | `agent-api-secret` / `hasuraAdminSecret` | Hasura admin secret. Only read when `api.hasura.enabled` is `true` (not surfaced by this stack) |
| `.../agent-ui` | `keycloak_client_secret` | `agent-ui-secret` / `keycloakClientSecret` | `Dare-TRE-UI` client secret (the older `ui` component) |
| `.../agent-web` | `better_auth_secret` | `agent-web-secret` / `betterAuthSecret` | Better Auth signing secret |
| `.../agent-web` | `keycloak_client_secret` | `agent-web-secret` / `keycloakClientSecret` | `Dare-TRE-UI` client secret (the primary `web` UI) |
| `.../credentials-camunda` | `connection_string_credentials` | `credentials-camunda-secret` / `connectionStringCredentials` | PostgreSQL connection string for `TRE_Credentials` on `pg-pooler` |
| `.../credentials-camunda` | `connection_string_tre_data` | `credentials-camunda-secret` / `connectionStringTreData` | Connection string to the **external** TRE data database (not deployed by this chart — see **CloudNativePG** below) |
| `.../credentials-camunda` | `ldap_admin_password` | `credentials-camunda-secret` / `ldapAdminPassword` | Bind password for the directory named by `agent.ldap.*` — the external AD in production, or `.../ldap`'s `admin_password` if `openldap.enabled` |
| `.../credentials-camunda` | `vault_token` | `credentials-camunda-secret` / `vaultToken` | Token for this stack's own Vault, used by the worker |
| `.../rustfs` | `access_key` | `agent-rustfs-secret` / `RUSTFS_ACCESS_KEY` | Must equal `.../agent-api`'s `s3_access_key` |
| `.../rustfs` | `secret_key` | `agent-rustfs-secret` / `RUSTFS_SECRET_KEY` | Must equal `.../agent-api`'s `s3_secret_key` |
| `.../rabbitmq` | (read directly by the operator's `secretBackend.vault`, not a VaultSecret) | RabbitMQ default user | See below |
| `.../seq` | `admin_password` | `seq-admin-password-secret` / `password` | Seq's own first-run admin password, not an Agent app secret |
| `.../ldap` | `admin_password` | `agent-openldap-secret` / `LDAP_ADMIN_PASSWORD` | Only needed if `openldap.enabled`. Must equal `.../credentials-camunda`'s `ldap_admin_password` |
| `.../ldap` | `config_password` | `agent-openldap-secret` / `LDAP_CONFIG_ADMIN_PASSWORD` | Only needed if `openldap.enabled` |
| `postgres.backups.vault.path` (not under `vault.secretPath` — a separate, backup-destination-specific path, set only once backups are enabled) | `postgres.backups.vault.accessKeyField`/`secretKeyField` | `postgres-secret` / `backupAccessKey`, `backupSecretKey` | CNPG's own `ObjectStore` S3 credentials. See **Backups** below |

### RabbitMQ: the default user needs management permissions

`rabbitmq.vaultDefaultUser` (default `true`) gates only the `RabbitmqCluster`'s
`secretBackend.vault` block — production keeps Vault-backed credentials. Set it `false` only
on a cluster with no Vault to read from; the RabbitMQ Cluster Operator then generates its own
`rabbitmq-default-user` Secret with a random password instead.

As with `submission-stack`, the Vault-supplied default user at
`{{ .Values.vault.secretPath }}/rabbitmq` must carry the `management` tag / administrator
permissions, not just messaging permissions, or the Agent api's own startup vhost/exchange/queue
setup fails silently on every restart.

## Cookies

`templates/agent.yaml` wires `ui.sslCookies: "{{ .Values.global.ingress.tls }}"` — secure
cookies require HTTPS end-to-end, so this follows `global.ingress.tls` rather than being its
own stack value.

## Keycloak

The `web` and `ui` components, and the api's own TRE-realm identity, all authenticate as the
single `Dare-TRE-UI` client. The external `Dare-TRE` realm at `global.oidc.authority` must
already have:

- **`Dare-TRE-UI`** — confidential client, used by `api`, `ui` and `web`. Its client secret
  fills `treKeycloakClientSecret`/`agent-ui-secret`'s and `agent-web-secret`'s
  `keycloakClientSecret`.
- **`Dare-TRE-API`** — a valid audience for tokens issued to `Dare-TRE-UI`
  (`api.oidc.validAudiences`), not a separately authenticating client in this chart.
- **`Dare-TRE-S3`** — expected in the realm design alongside the two clients above (this
  chart's rendered configuration does not itself reference it by name; verify its exact use
  against the realm import before deploying a new realm).

The api also validates tokens from the Submission product's **`Dare-Control`** realm
(`submission.oidcAuthority`), as **`Dare-Control-API`** — its client secret fills
`submissionKeycloakClientSecret`/`agent-api-secret`. This is a cross-realm trust: `Dare-Control`
belongs to the Submission deployment, not to this stack, and must already exist there.

`*KeyCloakSettings__Authority` renders as `<realm>/.well-known/openid-configuration`, matching
compose — deliberate: issuer validation is satisfied by the OIDC metadata's fetched `Issuer`
field, not a literal match against `Authority` (`Agent.Api/Program.cs:196-198,237,241`).

### External AD (or OpenLDAP)

The Credentials Camunda worker binds to a directory named by `agent.ldap.*`. Production points
these at a real Active Directory:

- `agent.ldap.host`/`port`/`useSsl` — the AD host and whether to use LDAPS.
- `agent.ldap.adminDn`/`baseDn`/`userOu` — the bind DN and search base.
- The bind password is `credentials-camunda-secret`'s `ldapAdminPassword`
  (`.../credentials-camunda`'s `ldap_admin_password` in Vault).

`openldap.enabled` (default `false`) deploys a local OpenLDAP as a stand-in — for testing only,
never the production path. Its defaults already match `agent.ldap.*`'s own defaults
(`host: openldap`, `port: 389`, `baseDn: dc=camundaephemeral,dc=local`), but this stack does
**not** wire them together automatically: enabling `openldap.enabled` does not change
`agent.ldap.*`. A deployment using the bundled OpenLDAP must leave `agent.ldap.*` at its
defaults (or set them to match) itself.

## GA4GH TES backend

`api.tesApiUrl`/`AgentSettings__TESKAPIURL` names the TES (Task Execution Service) backend the
Agent submits work to; the API POSTs task-creation requests straight to this URL and appends
`/{taskId}?view=BASIC` to poll it (`Agent.Api/DoAgentWork.cs:138,210`), so it must be the full
`tasks` collection endpoint. **The recommended production path is an external TES URL** — set
`agent.api.tesApiUrl` to it. `tesk.enabled` (default `false`) is the alternative: an in-cluster
TESK (GA4GH TES-K8s reference implementation) deployment, reproducing the same Harbor OCI chart
coordinates as `director-wfs`'s `tesk-standalone-stack`
(`harbor.ukserp.ac.uk/tesk/chart/tesk`, version `0.1.0`). Its `tesk-api` Service (static name,
not release-scoped — verified by rendering the pinned chart) listens on port `8080` at base path
`/ga4gh/tes/v1` (`OPENAPI_TASKEXECUTIONSERVICE_BASE_PATH`, same render). When `tesk.enabled` is
`true` and `agent.api.tesApiUrl` is empty, `templates/agent.yaml` derives
`http://tesk-api:8080/ga4gh/tes/v1/tasks`; an explicit `agent.api.tesApiUrl` always wins.

**For local TES testing, use `director-wfs.sh`** (`/Users/alex/Devel/feda/director-wfs`) to
bring up a disposable kind-based TESK environment — do not enable `tesk.enabled` here for that
purpose.

### TESK prerequisites this stack does not supply

Enabling `tesk.enabled` installs the `tesk` chart, but `director-wfs.sh` and its
`tesk-standalone-stack` chart create two more things out-of-band that this stack does not
reproduce. Verified by reading the pulled chart
(`oci://harbor.ukserp.ac.uk/tesk/chart/tesk:0.1.0`) and `director-wfs.sh`/
`tesk-standalone-stack/templates/tesk-configs.yaml`:

- **An `aws-secret` Secret**, keys `config` and `credentials` (AWS CLI-style INI content: an
  `[default]` section with `endpoint_url` in `config`, `aws_access_key_id`/
  `aws_secret_access_key` in `credentials`). The `tesk` chart only creates this Secret itself
  when `storage.authType` is `file`, reading the values from files baked into the chart
  package (`templates/storage/aws-secret.yaml`, gated `and (eq .Values.storage.type "s3") (eq
  .Values.storage.authType "file")`) — not usable from a `valuesObject`. This stack, like
  `director-wfs.sh`, sets `storage.authType: extraManifests` specifically to skip that
  chart-bundled path; **whoever enables `tesk.enabled` must create the `aws-secret` Secret in
  `global.namespace` themselves**, pointed at this stack's own `agent-rustfs-secret`
  credentials (`director-wfs.sh`'s `apply_tesk_aws_secret` function is the reference shape).
- **A `tesk-security-context-configmap` ConfigMap** (`data.securityContext`, e.g. `fsGroup:
  1000`) plus two `tesk.extraEnv` entries pointing taskmaster at it
  (`TESK_API_TASKMASTER_ENVIRONMENT_CONFIGMAP` and `CONFIGMAP`, both set to the ConfigMap's
  name). `director-wfs` creates this ConfigMap itself
  (`tesk-standalone-stack/templates/tesk-configs.yaml`) because its cluster runs Gatekeeper
  policies that require task-executor pods to carry a securityContext; this stack's
  `templates/tesk.yaml` does not create the ConfigMap or wire `tesk.extraEnv`.

**Evidence this is a documentation gap, not a startup-blocking one:** the `tesk` chart's own
`tesk-api`/taskmaster Deployment template does not reference either
`TESK_API_TASKMASTER_ENVIRONMENT_CONFIGMAP` or `CONFIGMAP` itself — they only reach the
container via `.Values.tesk.extraEnv`, a plain passthrough list the chart's Deployment template
appends verbatim. `helm template` against the pinned chart renders cleanly with neither set (no
missing-value errors, `tesk-api` Deployment present). The risk is downstream: taskmaster
launches one Kubernetes Job per TES task at runtime, and without the ConfigMap/extraEnv wiring
those per-task pods get no securityContext at all — likely rejected by this cluster's own
Pod Security admission if it enforces the org's usual non-root baseline (doc 03). **If you
enable `tesk.enabled` on a security-restricted namespace, create both the ConfigMap and set
`tesk.taskmasterImage`/`tesk.filerImage` alongside your own `aws-secret`, mirroring
`tesk-standalone-stack`'s two templates** — this stack intentionally keeps `tesk.yaml`'s
`valuesObject` minimal (per the brief) rather than reproducing director-wfs's cluster-hardening
layer (Gatekeeper, trust-manager, Falco) as well.

## CloudNativePG: two databases, one external

CNPG's `bootstrap.initdb` is left at its defaults, which creates a database and a role both
named `app`. Two declarative `Database` objects then create the real application databases,
owned by that same `app` role:

- **`dare-tre`** → `DARE-Tre`, the api's own database (`ConnectionStrings__DefaultConnection`).
- **`tre-credentials`** → `TRE_Credentials`, shared by `api` and the Credentials Camunda worker
  (`ConnectionStrings__CredentialsConnection`).

**Production's TRE data database is external.** `ConnectionStrings__TREPostgresConnection`
(the database the Camunda worker creates ephemeral credentials against) is not a `Database`
object in this chart — its connection string is `credentials-camunda-secret`'s
`connectionStringTreData`, pointed at wherever that TRE's own data database actually lives.

This needs the `Database` CRD, added in CloudNativePG 1.25 (same mechanism as
`submission-stack`).

## The shared `agent-processmodels` PVC needs an RWX storage class

`agent.processModels.accessModes` defaults to `[ReadWriteMany]` (api and camunda both mount
it, on a multi-node prod cluster), but is deployment-specific: a single-node kind cluster's
default provisioner is RWO-only, so local install overrides it to `[ReadWriteOnce]` (see
`agent-devstack`'s README). `global.storageClass` (`ceph-block`) is also typically RWO-only.
Set `agent.processModels.storageClassName` to the cluster's RWX-capable class (e.g. its
CephFS class) before deploying with the default `[ReadWriteMany]`, or the PVC will not
bind. Left `null` by default — the standalone chart then omits `storageClassName` entirely
and falls back to whatever the cluster's default class is, which will fail for
`ReadWriteMany` on a default class that is RWO-only.

## Backups

**Off by default for PostgreSQL** (`postgres.backups.enabled: false`): no destination S3 bucket
has been set up for this stack yet. **On by default for volumes**
(`global.veleroBackup.enabled: true`), which the shared prod cluster's Velero picks up by the
`persistentVolumeLabels` selector.

Turning `postgres.backups.enabled` on also requires the `barman-cloud.cloudnative-pg.io` CNPG
plugin installed in the cluster; `templates/postgres.yaml`'s `Cluster.spec.plugins` references
it by name but does not install it.

With today's defaults, real data sits in three places with different protection:

- **`postgres` (the CNPG `Cluster`)** — `DARE-Tre` and `TRE_Credentials`. Not backed up until
  `postgres.backups.enabled`, `destinationPath`, `endpointURL`, `endpointCASecretName` (a Secret
  with `ca.crt` for the destination's certificate — not created by this chart, must already
  exist) and `postgres.backups.vault.path`/`accessKeyField`/`secretKeyField` are all set. The
  S3 credentials themselves are **not** a separate Secret: they are a second
  `vaultSecretDefinitions` entry (aliased `backup`) on the same `postgres-secret` VaultSecret,
  reading `postgres.backups.vault.path` and filling `backupAccessKey`/`backupSecretKey` — the
  same mechanism `submission-stack` uses.
- **The `agent-processmodels` PVC** — the shared Camunda DMN/BPMN process models. Labelled with
  `persistentVolumeLabels`, covered by the Velero `Schedule` above (its selector is set from the
  same value, so the two cannot drift).
- **RustFS's own storage** — uploaded TRE files. Labelled via the rustfs chart's own
  `commonLabels` value, also covered by the Velero `Schedule`.

If `openldap.enabled` is turned on for anything beyond disposable testing: its own PVC (a
StatefulSet `volumeClaimTemplate`) is **not** covered by the Velero `Schedule` — the
`openldap-stack-ha` chart's `commonLabels` value does not reach `volumeClaimTemplates`. Treat
any enabled OpenLDAP as ephemeral/test-only data.

**The `camunda` Application's own PVCs — the Zeebe broker's data volume and Elasticsearch's —
are also not covered by the Velero `Schedule`.** `templates/camunda.yaml` does not pass
`persistentVolumeLabels` into the camunda-platform chart's `valuesObject` (same gap as
`airlock-stack`), so neither Zeebe's nor Elasticsearch's storage carries the label the
`Schedule`'s selector matches. This is consistent with the Credentials Camunda worker's data
model: process state lives in the two CNPG databases above, and Zeebe/Elasticsearch here hold
only in-flight workflow instance state, not the system of record.

## Values reference

### Global

| Name | Description | Default |
|---|---|---|
| `global.argoProject` | ArgoCD project every `Application` uses. | `agent` |
| `global.namespace` | Namespace every object in this stack lives in. | `5s-tes-agent` |
| `global.oidc.authority` | Full `Dare-TRE` realm URL, passed to the standalone chart. | `https://keycloak.example.ac.uk/realms/Dare-TRE` |
| `global.ingress.enabled` | Create Ingresses at all. | `true` |
| `global.ingress.host` | Base domain. `agent`/`agent-api`/`seq`/`rustfs`/`camunda`/`tesk` become subdomains of it. | `example.ac.uk` |
| `global.ingress.className` | Ingress controller class. | `nginx` |
| `global.ingress.certClusterIssuer` | cert-manager ClusterIssuer. | `ca-issuer` |
| `global.ingress.tls` | Terminate TLS at the ingress. | `true` |
| `global.storageClass` | Storage class for postgres, rabbitmq, rustfs, seq, vault, openldap. | `ceph-block` |
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
| `vault.role` | Vault role the cluster's Kubernetes auth uses. | `agent` |
| `vault.secretPath` | Parent path for every VaultSecret. | `kvv2/data/prod/prod/agent` |
| `vault.authPath` | Kubernetes-auth mount. | `kubernetes` |
| `vault.address` | This stack's own Vault Service address, wired into every VaultSecret's `connection.address`. See **Vault** above. | `http://vault:8200` |
| `vault.enabled` | Deploy this stack's own Vault `Application`. Runtime dependency (`api` and the Camunda worker call it directly), so this stays `true` even where `vault.secretsEnabled` is `false`. | `true` |
| `vault.secretsEnabled` | Deploy every `VaultSecret` under `templates/secrets/`. `false` only where something else provides those Secrets (e.g. the devstack's static Secrets). | `true` |
| `vault.repoURL` | Helm repo the Vault chart is pulled from. | `https://helm.releases.hashicorp.com` |
| `vault.chart` | Chart name within that repo. | `vault` |
| `vault.chartVersion` | hashicorp/vault chart version. | `0.34.1` |
| `vault.dataStorageSize` | Vault's own data PVC size. | `10Gi` |
| `vault.injector.enabled` | Enable the Vault Agent Injector webhook. | `false` |

### agent (own app)

| Name | Description | Default |
|---|---|---|
| `agent.enabled` | Create the `agent` `Application`. | `true` |
| `agent.chartVersion` | Version of the `agent` chart in Harbor. | `1.0.0` |
| `agent.imageVersion` | Image tag for `api`, `ui`, `web` and `camunda`. | `3.2.0` |
| `agent.api.publicUrl` | Public API URL embedded in TRE onboarding JSON. Empty computes one from `global.ingress`. | `""` |
| `agent.api.treName` | **REQUIRED for production.** Name of this TRE deployment. Empty leaves the standalone chart's dev default (`DEV`) in place. | `""` |
| `agent.api.tesApiUrl` | **REQUIRED for production** unless `tesk.enabled` is `true`. External TES backend URL — the recommended production path. Empty with `tesk.enabled: false` leaves the standalone chart's dev default (`http://localhost:8000/v1/tasks`), a broken TES endpoint once actually in-cluster; empty with `tesk.enabled: true` derives the in-cluster TESK URL instead. See **GA4GH TES backend** above. | `""` |
| `agent.web.publicUrl` | Public URL of the Next.js app, used by Better Auth. Empty computes one from `global.ingress`. | `""` |
| `agent.processModels.storageClassName` | RWX-capable storage class for the shared `agent-processmodels` PVC. See above. | `null` |
| `agent.processModels.accessModes` | Access mode(s) for the shared `agent-processmodels` PVC. Deployment-specific; see above. | `[ReadWriteMany]` |
| `agent.ldap.host`/`port`/`adminDn`/`baseDn`/`userOu`/`useSsl` | External AD (or `openldap.enabled`'s stand-in) connection settings for the Credentials Camunda worker. | see values.yaml |

### Submission cross-link

| Name | Description | Default |
|---|---|---|
| `submission.oidcAuthority` | Full `Dare-Control` realm URL. | `https://keycloak.example.ac.uk/realms/Dare-Control` |
| `submission.apiUrl` | Public URL of the Submission API. | `https://submission-api.example.ac.uk` |
| `submission.s3Url` | Submission product's S3 endpoint (not this stack's own rustfs). | `https://submission-rustfs.example.ac.uk` |

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
| `seq.requireAuthForIngestion` | Require an API key for HTTP log ingestion. No `seqApiKey` is wired into any component's Secret, so leave `false`. | `false` |
| `seq.resources.requests.cpu` | CPU request. | `250m` |
| `seq.resources.requests.memory` | Memory request. | `512Mi` |
| `seq.resources.limits.memory` | Memory limit. | `512Mi` |

### camunda

| Name | Description | Default |
|---|---|---|
| `camunda.enabled` | Deploy the `camunda` `Application`. | `true` |
| `camunda.repoURL` | Helm repo the camunda-platform chart is pulled from. | `https://helm.camunda.io` |
| `camunda.chart` | Chart name within that repo. | `camunda-platform` |
| `camunda.chartVersion` | camunda-platform chart version. Pinned to the same version `airlock-stack` pins. | `13.4.1` |

### openldap

| Name | Description | Default |
|---|---|---|
| `openldap.enabled` | Deploy a local OpenLDAP as a stand-in for an external AD. Testing only. | `false` |
| `openldap.repoURL` | Helm repo the chart is pulled from. | `https://jp-gouin.github.io/helm-openldap/` |
| `openldap.chart` | Chart name within that repo. | `openldap-stack-ha` |
| `openldap.chartVersion` | openldap-stack-ha chart version. | `4.3.3` |
| `openldap.replicas` | Replica count. | `1` |
| `openldap.storageSize` | Size of its data PVC. | `1Gi` |
| `openldap.ldapDomain` | Dot-form LDAP domain (the chart's `global.ldapDomain`). Keep the domain component in step with `agent.ldap.baseDn` if both are left at their defaults. | `camundaephemeral.local` |

### tesk

| Name | Description | Default |
|---|---|---|
| `tesk.enabled` | Deploy an in-cluster TESK. Alternative to an external TES URL — see **GA4GH TES backend** above. | `false` |
| `tesk.repoURL` | Harbor OCI chart repository. | `harbor.ukserp.ac.uk/tesk/chart` |
| `tesk.chart` | Chart name within that repository. | `tesk` |
| `tesk.chartVersion` | TESK chart version. | `0.1.0` |
| `tesk.image` | TESK API image. | `harbor.ukserp.ac.uk/tesk/tesk-api:1.0.1` |
| `tesk.taskmasterImage.name`/`.version` | Taskmaster sidecar image. | see values.yaml |
| `tesk.filerImage.name`/`.version` | Filer sidecar image. | see values.yaml |

### rabbitmq

| Name | Description | Default |
|---|---|---|
| `rabbitmq.replicas` | `RabbitmqCluster` replica count. | `1` |
| `rabbitmq.storageSize` | Size of the broker's data PVC. | `10Gi` |
| `rabbitmq.additionalConfig` | Extra `rabbitmq.conf` lines, passed to the operator verbatim. | `""` |
| `rabbitmq.vaultDefaultUser` | Default user credentials come from Vault, via the operator's own `secretBackend.vault`. `false` makes the operator generate its own `rabbitmq-default-user` Secret instead. See **RabbitMQ** above. | `true` |

### postgres

| Name | Description | Default |
|---|---|---|
| `postgres.database` | Name of the api's own database (the `dare-tre` `Database` object). See **CloudNativePG** above. | `DARE-Tre` |
| `postgres.credentialsDatabase` | Name of the shared Credentials database (the `tre-credentials` `Database` object). | `TRE_Credentials` |
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
