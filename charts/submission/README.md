# submission

Standalone chart for the Submission product.

## What this chart deploys

- **api** — the Submission API (`submission-api` image), listening on `/health` at port 8080.
- **ui** — the Submission web UI (`submission-ui` image), listening on `/health` at port 8080.

Both components share one PersistentVolumeClaim, `submission-dataprotection`, that holds
ASP.NET data-protection keys, and both stay at `replicas: 1` because that PVC is
`ReadWriteOnce` and the UI keeps its session store in memory. If `api.enabled` is
`false`, `submission-dataprotection` still renders: it is not owned by either component.
With the default `ReadWriteOnce` access mode, both pods must land on the same node —
fine on a single-node kind cluster, but a multi-node cluster must set
`dataProtection.accessModes: [ReadWriteMany]`, which the `submission-stack` chart does
for production.

## What must already exist

- The two Secrets listed below.
- A reachable Keycloak realm at `global.oidc.authority`.
- `SubmissionKeyCloakSettings__Authority` renders as `<realm>/` (trailing slash, no
  well-known suffix) and `__MetadataAddress` as `<realm>/.well-known/openid-configuration`,
  matching compose exactly — deliberate: `Submission.Api` sets
  `TokenValidationParameters.ValidateIssuer = false` (`Submission.Api/Program.cs:117`), so
  `Authority`'s shape has no bearing on issuer validation at all.
- A reachable RabbitMQ broker, PostgreSQL database, RustFS (S3-compatible) endpoint,
  Vault and Seq instance, at the addresses given by `api.rabbitmqHost`, the
  `connectionString` secret, `api.s3Url`, `api.vaultUrl` and `global.config.seqUrl`.
- The cluster CA bundle ConfigMap named by `global.trustClusterCa.configMapName`, if
  `global.trustClusterCa.enabled` is `true`.

## What this chart does not do

It does not install PostgreSQL, RabbitMQ, RustFS, Vault, Seq or Keycloak. It does not
create any Secret. It contains no ArgoCD `Application` and no `dependencies:`. The
`submission-stack` chart installs all of the above.

## Secrets

This chart does **not** create these. They must already exist in the namespace before
the chart is installed. The `submission-stack` chart creates them from Vault.

### `submission-api-secret`

Set by `api.secretName`.

| **Key** | **Used for** | **Required** |
|---|---|---|
| `connectionString` | PostgreSQL connection string, including the password. Read into `ConnectionStrings__DefaultConnection`. | Yes |
| `rabbitUsername` | RabbitMQ username. Read into `RabbitMQ__Username`. | Yes |
| `rabbitPassword` | RabbitMQ password. Read into `RabbitMQ__Password`. | Yes |
| `s3AccessKey` | Access key for the RustFS bucket. Read into `MinioSettings__AccessKey`. | Yes |
| `s3SecretKey` | Secret key matching `s3AccessKey`. Read into `MinioSettings__SecretKey`. | Yes |
| `keycloakClientSecret` | Client secret for the `Dare-Control-API` Keycloak client. Read into `SubmissionKeyCloakSettings__ClientSecret`. | Yes |
| `keycloakAdminUsername` | Username the API uses to call the Keycloak admin API. Read into `KeycloakAdmin__Username`. | Yes |
| `keycloakAdminPassword` | Password matching `keycloakAdminUsername`. Read into `KeycloakAdmin__Password`. | Yes |
| `vaultToken` | Vault token. Read into `VaultSettings__Token`. | Yes |

### `submission-ui-secret`

Set by `ui.secretName`.

| **Key** | **Used for** | **Required** |
|---|---|---|
| `keycloakClientSecret` | Client secret for the `Dare-Control-UI` Keycloak client. Read into `SubmissionKeyCloakSettings__ClientSecret`. | Yes |

## Parameters

### Common parameters

| **Name** | **Description** | **Value** |
|---|---|---|
| `nameOverride` | Replaces the chart name in object names. | `""` |
| `fullnameOverride` | Replaces the full prefix on every object name. | `"submission"` |
| `imagePullSecrets` | Secrets used to pull images from a private registry. | `[]` |
| `serviceAccount.create` | Create a service account for the pods. | `true` |
| `serviceAccount.annotations` | Annotations on the ServiceAccount. | `{}` |
| `serviceAccount.name` | Overrides the ServiceAccount name. Empty uses the chart's fullname. | `""` |
| `podSecurityContext` | Pod-level securityContext. `fsGroup`/`fsGroupChangePolicy` let UID 1000 (`securityContext.runAsUser`) write the shared `dataProtection` PVC regardless of the volume's own GID. | `{fsGroup: 1000, fsGroupChangePolicy: OnRootMismatch}` |
| `securityContext.runAsUser` | User ID both containers run as. | `1000` |
| `securityContext.runAsGroup` | Group ID both containers run as. | `1000` |
| `securityContext.runAsNonRoot` | Stop containers running as root. Do not change without a reason in the pull request. | `true` |
| `securityContext.readOnlyRootFilesystem` | Make the container filesystem read only. | `true` |
| `securityContext.allowPrivilegeEscalation` | Stop a process gaining more privileges than the one that started it. | `false` |
| `securityContext.capabilities.drop` | Linux capabilities dropped from both containers. | `["ALL"]` |
| `persistentVolumeLabels` | Labels put on `submission-dataprotection`. The cluster's backup tool uses these. Set to `{}` on a cluster with no such tool. | `{hiru.io/backup: "enabled"}` |

### DataProtection

`submission-dataprotection` is shared by both components; see **What this chart deploys**.

| **Name** | **Description** | **Value** |
|---|---|---|
| `dataProtection.keysPath` | Path both containers mount the PVC at, and the value of `DataProtectionSettings__KeysPath`. | `/keys` |
| `dataProtection.accessModes` | PVC access modes. | `["ReadWriteOnce"]` |
| `dataProtection.size` | PVC size. | `100Mi` |
| `dataProtection.storageClassName` | PVC storage class. `null` uses the cluster default. | `null` |

### Global parameters

Settings shared by more than one component. Defined once.

| **Name** | **Description** | **Value** |
|---|---|---|
| `global.tag` | Image tag used by a component that does not pin its own. `submission-api` and `submission-ui` are built and released together, so one tag covers both. | `"3.2.0"` |
| `global.config.aspnetEnvironment` | Value of `ASPNETCORE_ENVIRONMENT` in both components. | `"Development"` |
| `global.config.seqUrl` | Address of the Seq instance both components log to. Read into `Serilog__SeqServerUrl`. | `"http://seq:5341"` |
| `global.config.logLevel` | Default log level. Read into `Serilog__MinimumLevel__Default` and `Logging__LogLevel__Default`. | `"Information"` |
| `global.oidc.authority` | Full Keycloak realm URL both components authenticate against. Also the source of `SubmissionKeyCloakSettings__Server`/`__Protocol`/`__Realm` (the API's legacy in-code Keycloak calls), parsed out with `urlParse` rather than set separately. | `"http://keycloak/realms/Dare-Control"` |
| `global.monitoring.enabled` | Push metrics to a Prometheus Pushgateway. | `false` |
| `global.monitoring.pushgatewayUrl` | Pushgateway address, used when `global.monitoring.enabled` is `true`. | `""` |
| `global.ingress.enabled` | Create an Ingress for either component at all. | `true` |
| `global.ingress.className` | Ingress controller class for every Ingress. | `"nginx"` |
| `global.ingress.host` | Base domain. `api.ingress.host`/`ui.ingress.host` default to a subdomain of this when left empty. api and ui are separate public endpoints, each with their own Ingress and hostname. | `"localtest.me"` |
| `global.ingress.certClusterIssuer` | cert-manager ClusterIssuer that issues each Ingress's TLS certificate. | `"ca-issuer"` |
| `global.ingress.tls` | Terminate TLS at the ingress. Each Ingress declares its own certificate. | `true` |
| `global.trustClusterCa.enabled` | Mount a cluster CA bundle over both containers' trust store. Both components call Keycloak over HTTPS. | `false` |
| `global.trustClusterCa.configMapName` | ConfigMap holding the bundle. Provided by the cluster, not by this chart. | `overlay-castore` |
| `global.trustClusterCa.key` | Key inside that ConfigMap. Also used as the mount `subPath`. | `ca-certificates.crt` |
| `global.trustClusterCa.mountPath` | File replaced inside the container. Correct for Debian, Ubuntu and Alpine images. | `/etc/ssl/certs/ca-certificates.crt` |

### API parameters

| **Name** | **Description** | **Value** |
|---|---|---|
| `api.enabled` | Deploy the API component. | `true` |
| `api.image.repository` | Image for the API. | `harbor.federated-analytics.ac.uk/5s-tes/submission-api` |
| `api.image.tag` | Image tag. Falls back to `global.tag` when empty. | `""` |
| `api.image.pullPolicy` | Image pull policy for the API. | `IfNotPresent` |
| `api.containerPort` | Port the ASP.NET app listens on inside the container. | `8080` |
| `api.resources` | Container resource requests/limits. | `{}` |
| `api.service.type` | API Service type. | `ClusterIP` |
| `api.secretName` | Name of the Kubernetes Secret holding this component's secrets. See **Secrets** above. | `submission-api-secret` |
| `api.ingress.enabled` | Create an Ingress for the API. | `true` |
| `api.ingress.host` | Hostname for the API Ingress. Empty computes `submission-api.<global.ingress.host>`. | `""` |
| `api.rabbitmqHost` | RabbitMQ host address. Read into `RabbitMQ__HostAddress`. | `rabbitmq` |
| `api.s3Url` | In-cluster RustFS S3 endpoint. Read into `MinioSettings__Url`. | `http://rustfs-svc:9000` |
| `api.s3ConsoleUrl` | Public RustFS console URL shown to users. Read into `MinioSettings__AdminConsole`. | `http://localhost:9001` |
| `api.oidc.clientId` | Keycloak client ID for the API. | `Dare-Control-API` |
| `api.oidc.validAudiences` | Accepted token audiences. | `Dare-Control-UI,Dare-Control-API,Dare-Control-Minio` |
| `api.oidc.tokenRefreshSeconds` | Token refresh interval in seconds. | `"3600"` |
| `api.oidc.autoTrustKeycloakCert` | Trust Keycloak's certificate without validation. Keep `false`; use `global.trustClusterCa` instead. | `"false"` |
| `api.oidc.validIssuer` | Expected token issuer override. Empty uses the Authority. | `""` |
| `api.oidc.redirectUrl` | OIDC redirect URL override. | `""` |
| `api.oidc.useRedirectUrl` | Use `api.oidc.redirectUrl` instead of the request URL. | `"false"` |
| `api.oidc.proxy` | The API sits behind an egress proxy when calling Keycloak. | `"false"` |
| `api.oidc.proxyAddresUrl` | Proxy address, used when `api.oidc.proxy` is `"true"`. | `""` |
| `api.oidc.bypassProxy` | Comma-separated proxy bypass list. | `""` |
| `api.keycloakAdmin.serviceAccountRole` | Realm role granted to the Keycloak admin service account. | `dare-tre-admin` |
| `api.vaultUrl` | Vault base URL. Read into `VaultSettings__BaseUrl`. | `http://vault:8200` |
| `api.vault.timeoutSeconds` | Vault client timeout. | `30` |
| `api.vault.secretEngine` | Vault secret engine mount. | `secret` |
| `api.vault.enableRetry` | Retry failed Vault calls. | `true` |
| `api.vault.maxRetryAttempts` | Maximum Vault retry attempts. | `3` |
| `api.publicUrl` | Public URL embedded in TRE onboarding JSON. Read into `SubmissionApiUrl`. | `http://submission-api.localtest.me` |
| `api.features.seedDemoData` | Seed demo data on startup. | `"false"` |
| `api.keycloakDemoMode` | Run the API's Keycloak integration in demo mode. | `"false"` |
| `api.suppressAntiforgery` | Disable antiforgery checks. | `"false"` |
| `api.email.enabled` | Enable outbound email. | `"false"` |
| `api.extraEnv` | Rare one-off environment variables. Anything the app always needs is a named value above instead. | `[]` |

### UI parameters

| **Name** | **Description** | **Value** |
|---|---|---|
| `ui.enabled` | Deploy the UI component. | `true` |
| `ui.image.repository` | Image for the UI. | `harbor.federated-analytics.ac.uk/5s-tes/submission-ui` |
| `ui.image.tag` | Image tag. Falls back to `global.tag` when empty. | `""` |
| `ui.image.pullPolicy` | Image pull policy for the UI. | `IfNotPresent` |
| `ui.containerPort` | Port the ASP.NET app listens on inside the container. | `8080` |
| `ui.resources` | Container resource requests/limits. | `{}` |
| `ui.service.type` | UI Service type. | `ClusterIP` |
| `ui.secretName` | Name of the Kubernetes Secret holding this component's secrets. See **Secrets** above. | `submission-ui-secret` |
| `ui.ingress.enabled` | Create an Ingress for the UI. | `true` |
| `ui.ingress.host` | Hostname for the UI Ingress. Empty computes `submission.<global.ingress.host>`. | `""` |
| `ui.appName` | Display name shown in the UI. | `Five Safes TES` |
| `ui.keycloakDemoMode` | Run the UI's Keycloak integration in demo mode. | `"false"` |
| `ui.sslCookies` | Mark cookies secure. Requires HTTPS end-to-end if `true`. | `"false"` |
| `ui.httpsRedirect` | Redirect HTTP to HTTPS inside the app. Kept `false`; TLS terminates at the ingress. | `"false"` |
| `ui.suppressAntiforgery` | Disable antiforgery checks. | `"false"` |
| `ui.formio.treForm` | Form.io URL for the TRE onboarding form. | `https://rikojtsvfwnqslz.form.io/darecontrolendpoint` |
| `ui.formio.userForm` | Form.io URL for the user form. | `https://rikojtsvfwnqslz.form.io/darecontroluser` |
| `ui.formio.projectForm` | Form.io URL for the project form. | `https://rikojtsvfwnqslz.form.io/darecontrolproject` |
| `ui.urlSettings.queryImageSql` | Container image reference shown to users for the SQL query runner. | `ukserp/runsql:1.0.0` |
| `ui.urlSettings.queryImageGraphQL` | Container image reference shown to users for the GraphQL query runner. | `harbor.ukserp.ac.uk/dare-trefx/control-tre-hasura:1.33.1` |
| `ui.s3ConsoleUrl` | Public RustFS console URL shown to users. Read into `URLSettingsFrontEnd__S3BaseUrl`. | `http://localhost:9001` |
| `ui.urlSettings.s3BucketPath` | RustFS console path template appended to a bucket name. | `/rustfs/console/browser/?bucket=` |
| `ui.oidc.clientId` | Keycloak client ID for the UI. | `Dare-Control-UI` |
| `ui.oidc.redirectUrl` | OIDC redirect URL override. | `""` |
| `ui.oidc.useRedirectUrl` | Use `ui.oidc.redirectUrl` instead of the request URL. | `"false"` |
| `ui.oidc.proxy` | The UI sits behind an egress proxy when calling Keycloak. | `"false"` |
| `ui.oidc.proxyAddresUrl` | Proxy address, used when `ui.oidc.proxy` is `"true"`. | `""` |
| `ui.oidc.bypassProxy` | Comma-separated proxy bypass list. | `""` |
| `ui.extraEnv` | Rare one-off environment variables. Anything the app always needs is a named value above instead. | `[]` |
