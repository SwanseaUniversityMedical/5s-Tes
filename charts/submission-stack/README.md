# submission-stack

Production stack for the Submission product: the `submission` standalone chart, its
dependencies, and the VaultSecrets that supply their passwords. No Deployment, Service or
Ingress for C# code lives in this chart; that is all in `charts/submission`.

## What this stack deploys

| Component | What it is | Sync wave |
|---|---|---|
| `templates/vault.yaml` | ArgoCD `Application` `vault`, hashicorp/vault chart, standalone (file) mode | 1 |
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
  Requires CNPG **>= 1.25** for the `Database` CRD (verified present: `databases.postgresql.cnpg.io`
  ships in the cloudnative-pg chart version this cluster installs).
- **The RabbitMQ Cluster Operator**, for `templates/rabbitmq.yaml`'s `RabbitmqCluster`.
- **The redhatcop VaultSecret CRDs/operator**, for every object under `templates/secrets/`.
- **ArgoCD**, watching this namespace, with a project matching `global.argoProject`.
- **Velero**, in `global.veleroBackup.namespace`, if `global.veleroBackup.enabled` is `true`.
- A `monitoring.coreos.com` PodMonitor CRD (Prometheus Operator), if `global.monitoring.enabled`
  is `true`.

## Vault

This stack deploys its own Vault instance (`templates/vault.yaml`) to hold Submission's
own application secrets, separate from the platform's Keycloak/Harbor Vault on the
`management` cluster. **This is a deliberate design decision for this stack and has not
been confirmed against precedent** — neither `serp-provisioning-stack` nor `airlock-stack`
deploy their own Vault; both read from an already-running Vault instead. Confirm this is
really wanted before this chart goes to production.

It starts sealed, using file storage (`server.standalone`, explicitly not `server.dev`).
Bring it up by hand the first time:

```bash
kubectl exec -n <namespace> -it vault-0 -- vault operator init
# Record the five unseal keys and the root token somewhere safe (not Git).
kubectl exec -n <namespace> -it vault-0 -- vault operator unseal   # x3, different keys
```

After init, write the app's own Vault token into Vault itself, at
`{{ .Values.vault.secretPath }}/submission-api`, key `vault_token`, so the
`submission-api-secret` VaultSecret can inject it as `vaultToken` (read by
`VaultSettings__Token`, used by the app's own runtime calls to this Vault instance via
`IVaultCredentialsService`).

### Vault paths (under `vault.secretPath`, default `kvv2/data/prod/prod/submission`)

The standalone chart's README lists the Kubernetes Secret names and keys each of these
fills; the two must agree.

| Path | Field | Fills | Used for |
|---|---|---|---|
| `.../postgres` | `postgres_password` | `postgres-secret` / `password` | CNPG superuser password |
| `.../submission-api` | `connection_string` | `submission-api-secret` / `connectionString` | Full PostgreSQL connection string, assembled in Vault |
| `.../submission-api` | `keycloak_client_secret` | `submission-api-secret` / `keycloakClientSecret` | `Dare-Control-API` client secret |
| `.../submission-api` | `keycloak_admin_username` | `submission-api-secret` / `keycloakAdminUsername` | `dare-control-realm-user` username |
| `.../submission-api` | `keycloak_admin_password` | `submission-api-secret` / `keycloakAdminPassword` | `dare-control-realm-user` password |
| `.../submission-api` | `s3_access_key` | `submission-api-secret` / `s3AccessKey` | RustFS access key. Must equal `.../rustfs`'s `access_key`. |
| `.../submission-api` | `s3_secret_key` | `submission-api-secret` / `s3SecretKey` | RustFS secret key. Must equal `.../rustfs`'s `secret_key`. |
| `.../submission-api` | `rabbit_username` | `submission-api-secret` / `rabbitUsername` | RabbitMQ default user (see below) |
| `.../submission-api` | `rabbit_password` | `submission-api-secret` / `rabbitPassword` | RabbitMQ default user password |
| `.../submission-api` | `vault_token` | `submission-api-secret` / `vaultToken` | Token for this stack's own Vault |
| `.../submission-ui` | `keycloak_client_secret` | `submission-ui-secret` / `keycloakClientSecret` | `Dare-Control-UI` client secret |
| `.../rustfs` | `access_key` | `submission-rustfs-secret` / `RUSTFS_ACCESS_KEY` | Must equal `.../submission-api`'s `s3_access_key` |
| `.../rustfs` | `secret_key` | `submission-rustfs-secret` / `RUSTFS_SECRET_KEY` | Must equal `.../submission-api`'s `s3_secret_key` |
| `.../rabbitmq` | (read directly by the operator's `secretBackend.vault`, not a VaultSecret) | RabbitMQ default user | See below |

### RabbitMQ: the default user needs management permissions

`Shared/FiveSafesTes.Core/Rabbit/SetUpRabbitMQ.cs` connects with
`EasyNetQ.Management.Client`'s `ManagementClient(hostname, username, password)`, which
defaults to **port 15672 over plain HTTP** — the RabbitMQ **management HTTP API**, not
AMQP (5672). At startup the API creates a vhost, an exchange and a queue through that API.
The Vault-supplied default user at `{{ .Values.vault.secretPath }}/rabbitmq` (read by the
`RabbitmqCluster`'s `secretBackend.vault`, and by `submission-api-secret`'s
`rabbit_username`/`rabbit_password`) must therefore carry the `management` tag /
administrator permissions, not just messaging permissions, or vhost/exchange/queue setup
fails silently on every restart.

## Keycloak

The external realm at `global.oidc.authority` must already have:

- **`Dare-Control-API`** — confidential client, service accounts enabled. Its service
  account needs the `dare-tre-admin` realm role (`api.keycloakAdmin.serviceAccountRole`
  in the standalone chart) for the endpoints under `[Authorize(Roles = "dare-tre-admin")]`.
- **`Dare-Control-UI`** — the UI's OIDC client.
- **A `dare-control-realm-user` admin user** — a realm user (not a service account) the
  API logs in as to call the Keycloak Admin REST API (`KeycloakAdminService`). Its
  username/password are `keycloakAdminUsername`/`keycloakAdminPassword` above.

## CloudNativePG: why a `Database` object, not `bootstrap.initdb.database`

CNPG's `bootstrap.initdb` is left at its defaults, which creates a database and a role
both named `app`. A declarative `Database` object, `dare-control`, then creates the real
application database, named `DARE-Control` (hyphenated, mixed case — intentional, for
compatibility with existing connection strings), owned by that same `app` role. This
needs the `Database` CRD, added in CloudNativePG 1.25; verified present in the
`cloudnative-pg` operator chart this cluster installs (`databases.postgresql.cnpg.io`
ships in the CRD bundle at the version `dev-env-setup/cluster-setup.sh` installs, app
version 1.30.0).

## Backups

**Off by default for PostgreSQL** (`postgres.backups.enabled: false`): no destination S3
bucket has been set up for this stack yet. **On by default for volumes**
(`global.veleroBackup.enabled: true`), which the shared prod cluster's Velero picks up
by the `persistentVolumeLabels` selector.

With today's defaults, real data sits in three places with different protection:

- **`postgres` (the CNPG `Cluster`)** — its own data. Not backed up until
  `postgres.backups.enabled`, `destinationPath`, `endpointURL`, `existingSecret` (an
  S3-credentials Secret with `ACCESS_KEY_ID`/`ACCESS_SECRET_KEY`) and
  `endpointCASecretName` (a Secret with `ca.crt` for the destination's certificate) are
  all set — none of these render a `Certificate`; the Secret must already exist.
- **The `submission-dataprotection` PVC** — ASP.NET data-protection keys. Labelled with
  `persistentVolumeLabels`, so covered by the Velero `Schedule` above.
- **RustFS's own storage** — uploaded files. The RustFS chart's PVCs are **not** labelled
  with `persistentVolumeLabels` by this chart (the chart's `commonLabels` value was not
  wired), so they are not covered by the Velero `Schedule` either, even though
  `global.veleroBackup.enabled` is `true`. Confirm whether this needs fixing before real
  files land there.

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
| `global.persistentVolumeLabels` | Labels passed to the standalone chart's PVC and the `Schedule`'s selector. | `{hiru.io/backup: "enabled"}` |
| `global.monitoring.enabled` | Push metrics to a Pushgateway; create PodMonitors. | `true` |
| `global.monitoring.pushgatewayUrl` | Pushgateway address. | see values.yaml |

### Vault

| Name | Description | Default |
|---|---|---|
| `vault.role` | Vault role the cluster's Kubernetes auth uses. | `submission` |
| `vault.secretPath` | Parent path for every VaultSecret. | `kvv2/data/prod/prod/submission` |
| `vault.authPath` | Kubernetes-auth mount. | `kubernetes` |
| `vault.enabled` | Deploy this stack's own Vault `Application`. | `true` |
| `vault.chartVersion` | hashicorp/vault chart version. | `0.34.1` |
| `vault.dataStorageSize` | Vault's own data PVC size. | `10Gi` |
| `vault.injector.enabled` | Enable the Vault Agent Injector webhook. | `false` |

### submission (own app)

| Name | Description | Default |
|---|---|---|
| `submission.enabled` | Create the `submission` `Application`. | `true` |
| `submission.chartVersion` | Version of the `submission` chart in Harbor. Not published yet — the first real publish lands via Task 4.1's CI; `1.0.0` is a placeholder pin. | `1.0.0` |
| `submission.imageVersion` | Image tag for both `submission-api` and `submission-ui`. | `3.2.0` |
| `submission.s3ConsoleUrl` | Public RustFS console URL. Empty computes one from `global.ingress`. | `""` |
| `submission.api.publicUrl` | Public API URL embedded in TRE onboarding JSON. Empty computes one from `global.ingress`. | `""` |

### rustfs / seq / vault / rabbitmq / postgres

See `values.yaml`; each block's keys map to the template file of the same name.
