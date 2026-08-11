# submission

Standalone chart for the 5S TES Submission layer.

## What this chart deploys

- `api` — Submission.Api, the researcher-facing submission API. Also consumes RabbitMQ messages and provisions per-project S3 users.
- `ui` — Submission.Web, the MVC frontend for the API.

## What must already exist

- The Secrets listed below, in the release namespace.
- A ConfigMap holding the cluster CA bundle (see `trustClusterCa`).
- A ReadWriteMany-capable storage class for the shared Data Protection claim.

## What this chart does not do

It does not install PostgreSQL, Keycloak, RabbitMQ, Vault, Seq or the S3
object store. It expects their addresses as values. The `submission-stack`
chart installs those and points this chart at them.

## Secrets

This chart does **not** create these. They must already exist in the
namespace before the chart is installed. In our deployments the
`submission-stack` chart creates them from Vault.

### `submission-api-secret`

Set by `api.secretName`.

| **Key** | **Used for** | **Required** |
|---|---|---|
| `connectionString` | Full PostgreSQL connection string for the `DARE-Control` database, including the password. Read into `ConnectionStrings__DefaultConnection`. | Yes |
| `keycloakClientSecret` | Client secret for the client named in `api.keycloak.clientId`. | Yes |
| `keycloakAdminPassword` | Password for the Keycloak admin account the API uses to create users and clients. Pairs with `api.keycloak.adminUsername`. | Yes |
| `rabbitPassword` | Password for the RabbitMQ user named in `global.config.rabbitmq.username`. | Yes |
| `s3AccessKey` | Access key for the Submission object store at `api.s3.url`. | Yes |
| `s3SecretKey` | Secret key matching `s3AccessKey`. | Yes |
| `vaultToken` | Token for the Vault at `global.config.vault.url`, used to store per-project S3 credentials. | Yes |

### `submission-ui-secret`

Set by `ui.secretName`.

| **Key** | **Used for** | **Required** |
|---|---|---|
| `keycloakClientSecret` | Client secret for the client named in `ui.keycloak.clientId`. | Yes |

## Parameters

### Common parameters

| **Name** | **Description** | **Value** |
|---|---|---|
| `nameOverride` | Replaces the chart name in object names. | `""` |
| `fullnameOverride` | Replaces the full prefix on every object name. The stack chart sets this so service DNS names stay predictable. | `"submission"` |
| `imagePullSecrets` | Secrets used to pull images from a private registry. | `[]` |
| `serviceAccount.create` | Create a service account for the pods. | `true` |
| `serviceAccount.annotations` | Annotations for the service account. | `{}` |
| `serviceAccount.name` | Use an existing service account name instead. | `""` |
| `podSecurityContext.fsGroup` | Group that owns mounted volumes, so the non-root app user can write the shared key ring. | `1000` |
| `securityContext.*` | Organisation security baseline (non-root, read-only filesystem, no capabilities). Do not weaken without a reason in the pull request. | see values |

### Certificate trust

| **Name** | **Description** | **Value** |
|---|---|---|
| `trustClusterCa.enabled` | Mount a cluster CA bundle over the container trust store. Turn off only on a cluster that has no such bundle. | `true` |
| `trustClusterCa.configMapName` | ConfigMap holding the bundle. Provided by the cluster, not by this chart. | `overlay-castore` |
| `trustClusterCa.key` | Key inside that ConfigMap. Also used as the mount `subPath`. | `ca-certificates.crt` |
| `trustClusterCa.mountPath` | File replaced inside the container. Correct for Debian, Ubuntu and Alpine images. | `/etc/ssl/certs/ca-certificates.crt` |

### Storage

| **Name** | **Description** | **Value** |
|---|---|---|
| `persistentVolumeLabels` | Labels put on every PersistentVolumeClaim, for the cluster backup tool. Set `{}` for none. | `hiru.io/backup: "enabled"` |
| `dataProtectionStorage.size` | Size of the shared ASP.NET Data Protection key claim. | `1Gi` |
| `dataProtectionStorage.storageClassName` | Storage class for that claim. Must support ReadWriteMany. `null` uses the cluster default. | `null` |
| `dataProtectionStorage.mountPath` | Where the key ring is mounted. Fixed in the application code; do not change without a matching code change. | `/root/.aspnet/DataProtection-Keys` |

### Monitoring

| **Name** | **Description** | **Value** |
|---|---|---|
| `monitoring.enabled` | Push Prometheus metrics from both components to a pushgateway. | `true` |
| `monitoring.pushgatewayUrl` | Pushgateway address, including the `/metrics` path. | `http://prometheus-pushgateway.hiru-mgmt-monitoring.svc.cluster.local:9091/metrics` |

### Global parameters

| **Name** | **Description** | **Value** |
|---|---|---|
| `global.tag` | Image tag used by any component that does not pin its own. | `latest` |
| `global.ingress.enabled` | Master switch for every Ingress in the chart. | `true` |
| `global.ingress.className` | Ingress controller class. | `nginx` |
| `global.ingress.certClusterIssuer` | cert-manager ClusterIssuer that issues the TLS certificates. | `ca-issuer` |
| `global.ingress.tls` | Render the TLS blocks. Turn off for local clusters with no issuer. | `true` |
| `global.config.seqUrl` | Address of the Seq instance both components log to. | `http://seq:5341` |
| `global.config.logLevel` | Serilog minimum level for both components. | `Information` |
| `global.config.keycloak.url` | Base URL of the Keycloak that holds the Submission (control) realm. Wrong value means nobody can log in. | `http://keycloak.localtest.me:8085` |
| `global.config.keycloak.realm` | Name of that realm. | `Dare-Control` |
| `global.config.keycloakDemoMode` | Keycloak demo mode flag. Keep off outside demos. | `false` |
| `global.config.uiRedirectUrl` | Public URL of the UI. Keycloak sends browsers back here after login; must match the UI ingress host. | `http://submission.localtest.me` |
| `global.config.suppressAntiforgery` | Disables anti-forgery tokens and switches Data Protection onto the shared claim. Testing only. | `false` |
| `global.config.sslCookies` | Mark auth cookies secure. Needs HTTPS end to end. | `false` |
| `global.config.httpsRedirect` | Redirect HTTP to HTTPS inside the app. Usually off behind an ingress that already does this. | `false` |
| `global.config.rabbitmq.host` | RabbitMQ host both components use. | `rabbitmq` |
| `global.config.rabbitmq.username` | RabbitMQ username. Password comes from the API secret. | `rabbitmq` |
| `global.config.vault.url` | Address of the Vault the API stores per-project S3 credentials in. | `http://vault:8200` |
| `global.config.proxy.enabled` | Route Keycloak HTTP calls through a proxy. | `false` |
| `global.config.proxy.url` | Proxy address, when enabled. | `""` |
| `global.config.proxy.bypass` | Comma-separated hosts that skip the proxy. | `submission-api,seq` |

### API parameters

| **Name** | **Description** | **Value** |
|---|---|---|
| `api.enabled` | Deploy the API component. | `true` |
| `api.image.repository` | Image for the API. | `harbor.federated-analytics.ac.uk/5s-tes/submission-api` |
| `api.image.tag` | Image tag. Falls back to `global.tag` when empty. | `""` |
| `api.image.pullPolicy` | Image pull policy. | `IfNotPresent` |
| `api.replicas` | Number of copies. | `1` |
| `api.containerPort` | Port the ASP.NET app listens on. Describes the app; it does not change it. | `8080` |
| `api.resources` | CPU and memory requests/limits. | `{}` |
| `api.service.type` | Service type. | `ClusterIP` |
| `api.secretName` | Name of the Kubernetes Secret holding the API secrets. See **Secrets**. | `submission-api-secret` |
| `api.ingress.enabled` | Create an Ingress for the API. | `true` |
| `api.ingress.host` | Hostname for that Ingress. | `submission-api.localtest.me` |
| `api.publicUrl` | Public URL of this API. Embedded in TRE onboarding data, so remote agent hosts must be able to reach it. An in-cluster name breaks onboarding. | `http://submission-api.localtest.me` |
| `api.keycloak.clientId` | Keycloak client the API authenticates as. | `Dare-Control-API` |
| `api.keycloak.validAudiences` | Audiences accepted in tokens. | `Dare-Control-UI,Dare-Control-API,Dare-Control-Minio` |
| `api.keycloak.server` | Keycloak host, as the API validates issuers against it. | `keycloak.localtest.me` |
| `api.keycloak.protocol` | Scheme for that host. | `http` |
| `api.keycloak.validIssuer` | Exact issuer string expected in tokens. Wrong value rejects every login. | `http://keycloak.localtest.me:8085/realms/Dare-Control` |
| `api.keycloak.autoTrustKeycloakCert` | Trust a self-signed Keycloak certificate. Prefer `trustClusterCa` instead. | `false` |
| `api.keycloak.signedOutRedirectUri` | Where browsers land after sign-out. | `http://submission.localtest.me` |
| `api.keycloak.tokenRefreshSeconds` | Token refresh interval. | `300` |
| `api.keycloak.adminUsername` | Keycloak admin account used to create users and clients. | `admin` |
| `api.s3.url` | Address of the Submission object store. | `http://rustfs-submission:9000` |
| `api.s3.adminConsole` | Address of that store's admin console. | `http://rustfs-submission:9001` |
| `api.seedDemoData` | Seed demo data on start. Demos only. | `false` |

### UI parameters

| **Name** | **Description** | **Value** |
|---|---|---|
| `ui.enabled` | Deploy the UI component. | `true` |
| `ui.image.repository` | Image for the UI. | `harbor.federated-analytics.ac.uk/5s-tes/submission-ui` |
| `ui.image.tag` | Image tag. Falls back to `global.tag` when empty. | `""` |
| `ui.image.pullPolicy` | Image pull policy. | `IfNotPresent` |
| `ui.replicas` | Number of copies. | `1` |
| `ui.containerPort` | Port the ASP.NET app listens on. | `8080` |
| `ui.resources` | CPU and memory requests/limits. | `{}` |
| `ui.service.type` | Service type. | `ClusterIP` |
| `ui.secretName` | Name of the Kubernetes Secret holding the UI secrets. See **Secrets**. | `submission-ui-secret` |
| `ui.ingress.enabled` | Create an Ingress for the UI. | `true` |
| `ui.ingress.host` | Hostname for that Ingress. Must match `global.config.uiRedirectUrl`. | `submission.localtest.me` |
| `ui.keycloak.clientId` | Keycloak client the UI logs users in with. | `Dare-Control-UI` |
| `ui.apiAddress` | Address the UI calls the API on. The in-chart service name works unless the API is elsewhere. | `http://submission-api` |
| `ui.uiName` | Product name shown in the UI. | `Five Safes TES` |
| `ui.frontend.queryImageSql` | Container image reference the SQL query wizard submits as a task. | `harbor.federated-analytics.ac.uk/5s-tes-analysis-tools/5s-tes-analysis-tools-tre-sqlpg:1.0.0` |
| `ui.frontend.s3BaseUrl` | Browser-facing URL of the S3 console, for output links. | `http://rustfs-submission.localtest.me` |
| `ui.frontend.s3BucketPath` | Console path template for a bucket link. | `/rustfs/console/browser/?bucket=` |
