# agent

Standalone chart for the Agent product.

## What this chart deploys

- **api** — the TRE Agent API (`agent-api` image), listening on `/health` at port 8080.
- **ui** — the TRE Agent web UI (`agent-ui` image, .NET MVC `Agent.Web`), listening on
  `/health` at port 8080. This is the primary user-facing UI, at
  `agent.<global.ingress.host>`.
- **camunda** — the Credentials Camunda worker (`credentials-camunda` image), a headless
  Zeebe job worker. It has no Service and no Ingress: nothing calls it directly.

`api` and `camunda` share one PersistentVolumeClaim, `agent-processmodels`, holding the
Camunda DMN/BPMN process models, and both stay at `replicas: 1` because that PVC is
`ReadWriteOnce`. `ui` also stays at `replicas: 1` because it keeps its session store in
memory (`MemoryCacheTicketStore`). With the default `ReadWriteOnce` access mode, `api` and
`camunda` must land on the same node — fine on a single-node kind cluster, but a
multi-node cluster must set `processModels.accessModes: [ReadWriteMany]`.

Both `api` and `camunda`'s Deployments run a `seed-process-models` initContainer that
copies the image's own `/app/ProcessModels` into the shared PVC the first time it has not
yet been seeded (both images bake in the same files). The initContainer mounts the PVC at
`/models`, not `/app/ProcessModels`, so it can read the image's baked-in files as the copy
source without the (empty, on first boot) PVC hiding them. The seed check looks for the
sentinel file `/models/credentials.dmn` rather than testing directory emptiness, so it is
immune to a stray `lost+found` entry on an ext4-formatted volume; the copy writes every
other file first and writes `credentials.dmn` last, atomically (copy to a per-pod temp name,
`.credentials.dmn.tmp.$HOSTNAME`, then `mv` into place), so a container killed mid-copy — or
mid-write of the sentinel itself — leaves the sentinel absent and the next run retries, and
two pods racing on first boot don't collide on the same temp file. It is idempotent, so it is
safe to run unconditionally from both components — `api` alone still seeds the PVC correctly
when `camunda.enabled` is `false`. `api` and `camunda`'s main containers then both mount the
PVC at `/app/ProcessModels`. Because the seed check only looks for `credentials.dmn`, a later
image that adds new model files without touching that sentinel will not get them copied to an
already-seeded PVC — delete the sentinel or the PVC to force a reseed.

## What must already exist

- The three Secrets listed below.
- A reachable Keycloak realm at `global.oidc.authority` (Dare-TRE) and, for the
  cross-stack settings, the Submission product's Keycloak realm, API and S3 endpoint at
  `submission.oidcAuthority`/`submission.apiUrl`/`submission.s3Url`.
- `*KeyCloakSettings__Authority`/`__MetadataAddress` render as `<realm>/.well-known/openid-configuration`,
  matching compose exactly — deliberate: issuer validation is satisfied by the OIDC metadata's
  fetched `Issuer` field, not by a literal string match against `Authority` (`Agent.Api/Program.cs:196-198,237,241`).
- A reachable RabbitMQ broker, PostgreSQL database(s), RustFS (S3-compatible) endpoint,
  Vault, Seq, Zeebe gateway and OpenLDAP (or external AD) directory, at the addresses
  given by `api.rabbitmqHost`, the `connectionString*` secrets, `api.s3Url`,
  `global.config.vaultUrl`, `global.config.seqUrl`, `global.config.zeebeGatewayAddress`
  and `camunda.ldap.*`.
- The cluster CA bundle ConfigMap named by `global.trustClusterCa.configMapName`, if
  `global.trustClusterCa.enabled` is `true`.

## What this chart does not do

It does not install PostgreSQL, RabbitMQ, RustFS, Vault, Seq, Zeebe, OpenLDAP or
Keycloak. It does not create any Secret. It contains no ArgoCD `Application` and no
`dependencies:`. The `agent-stack` chart installs all of the above.

## Secrets

This chart does **not** create these. They must already exist in the namespace before
the chart is installed. The `agent-stack` chart creates them from Vault.

### `agent-api-secret`

Set by `api.secretName`.

| **Key** | **Used for** | **Required** |
|---|---|---|
| `connectionStringDefault` | PostgreSQL connection string for the Agent database. Read into `ConnectionStrings__DefaultConnection`. | Yes |
| `connectionStringCredentials` | PostgreSQL connection string for the shared Credentials database. Read into `ConnectionStrings__CredentialsConnection`. | Yes |
| `rabbitUsername` | RabbitMQ username. Read into `RabbitMQ__Username`. | Yes |
| `rabbitPassword` | RabbitMQ password. Read into `RabbitMQ__Password`. | Yes |
| `treKeycloakClientSecret` | Client secret for the `Dare-TRE-UI` Keycloak client. Read into `TreKeyCloakSettings__ClientSecret`. | Yes |
| `submissionKeycloakClientSecret` | Client secret for the `Dare-Control-API` Keycloak client. Read into `SubmissionKeyCloakSettings__ClientSecret`. | Yes |
| `egressKeycloakClientSecret` | Client secret for the `Data-Egress-API` Keycloak client. Read into `DataEgressKeyCloakSettings__ClientSecret`. Only used when `api.egress.enabled` is `true`. | Only if `api.egress.enabled` |
| `s3AccessKey` | Access key for the TRE RustFS bucket. Read into `MinioTRESettings__AccessKey`. | Yes |
| `s3SecretKey` | Secret key matching `s3AccessKey`. Read into `MinioTRESettings__SecretKey`. | Yes |
| `vaultToken` | Vault token. Read into `VaultSettings__Token`. | Yes |
| `encryptionKey` | Base64 encryption key. Read into `EncryptionSettings__Key`. | Yes |
| `hangfireUsername` | Hangfire dashboard username. Read into `Hangfire__Username`. | Yes |
| `hangfirePassword` | Hangfire dashboard password. Read into `Hangfire__Password`. | Yes |
| `hasuraAdminSecret` | Hasura admin secret. Read into `HasuraSettings__HasuraAdminSecret`. Only used when `api.hasura.enabled` is `true`. | Only if `api.hasura.enabled` |

### `agent-ui-secret`

Set by `ui.secretName`.

| **Key** | **Used for** | **Required** |
|---|---|---|
| `keycloakClientSecret` | Client secret for the `Dare-TRE-UI` Keycloak client. Read into `TreKeyCloakSettings__ClientSecret`. | Yes |

### `credentials-camunda-secret`

Set by `camunda.secretName`.

| **Key** | **Used for** | **Required** |
|---|---|---|
| `ldapAdminPassword` | OpenLDAP (or external AD) admin bind password. Read into `LdapSettings__AdminPassword`. | Yes |
| `vaultToken` | Vault token. Read into `VaultSettings__Token`. | Yes |
| `connectionStringCredentials` | PostgreSQL connection string for the shared Credentials database. Read into `ConnectionStrings__CredentialsConnection`. | Yes |
| `connectionStringTreData` | PostgreSQL connection string the worker uses to create ephemeral credentials against a TRE data database. Read into `ConnectionStrings__TREPostgresConnection`. | Yes |

## Parameters

### Common parameters

| **Name** | **Description** | **Value** |
|---|---|---|
| `nameOverride` | Replaces the chart name in object names. | `""` |
| `fullnameOverride` | Replaces the full prefix on every object name. | `"agent"` |
| `imagePullSecrets` | Secrets used to pull images from a private registry. | `[]` |
| `serviceAccount.create` | Create a service account for the pods. | `true` |
| `serviceAccount.annotations` | Annotations on the ServiceAccount. | `{}` |
| `serviceAccount.name` | Overrides the ServiceAccount name. Empty uses the chart's fullname. | `""` |
| `podSecurityContext` | Pod-level securityContext. `fsGroup`/`fsGroupChangePolicy` let UID 1000 (`securityContext.runAsUser`) write the shared `agent-processmodels` PVC regardless of the volume's own GID. | `{fsGroup: 1000, fsGroupChangePolicy: OnRootMismatch}` |
| `securityContext.runAsUser` | User ID every container runs as. | `1000` |
| `securityContext.runAsGroup` | Group ID every container runs as. | `1000` |
| `securityContext.runAsNonRoot` | Stop containers running as root. Do not change without a reason in the pull request. | `true` |
| `securityContext.readOnlyRootFilesystem` | Make the container filesystem read only. | `true` |
| `securityContext.allowPrivilegeEscalation` | Stop a process gaining more privileges than the one that started it. | `false` |
| `securityContext.capabilities.drop` | Linux capabilities dropped from every container. | `["ALL"]` |
| `persistentVolumeLabels` | Labels put on `agent-processmodels`. The cluster's backup tool uses these. Set to `{}` on a cluster with no such tool. | `{hiru.io/backup: "enabled"}` |

### ProcessModels

`agent-processmodels` is shared by `api` and `camunda`; see **What this chart deploys**.

| **Name** | **Description** | **Value** |
|---|---|---|
| `processModels.accessModes` | PVC access modes. | `["ReadWriteOnce"]` |
| `processModels.size` | PVC size. | `100Mi` |
| `processModels.storageClassName` | PVC storage class. `null` uses the cluster default. | `null` |

### Global parameters

Settings shared by more than one component. Defined once.

| **Name** | **Description** | **Value** |
|---|---|---|
| `global.tag` | Image tag used by a component that does not pin its own. All four images are built and released together, so one tag covers them. | `"3.2.0"` |
| `global.config.aspnetEnvironment` | Value of `ASPNETCORE_ENVIRONMENT` in `api` and `ui`. | `"Development"` |
| `global.config.seqUrl` | Address of the Seq instance every .NET component logs to. Read into `Serilog__SeqServerUrl`. | `"http://seq:5341"` |
| `global.config.logLevel` | Default log level. Read into `Serilog__MinimumLevel__Default` and `Logging__LogLevel__Default`. | `"Information"` |
| `global.config.vaultUrl` | Vault base URL, used by `api` and `camunda`. Read into `VaultSettings__BaseUrl`. | `"http://agent-vault:8200"` |
| `global.config.zeebeGatewayAddress` | Zeebe gateway address, used by `api` and `camunda`. Read into `ZeebeBootstrap__Client__GatewayAddress`. | `"camunda-zeebe-gateway:26500"` |
| `global.oidc.authority` | Full Dare-TRE realm URL every component authenticates against. | `"http://keycloak/realms/Dare-TRE"` |
| `global.monitoring.enabled` | Push metrics to a Prometheus Pushgateway from `api`, `ui` and `camunda`. | `false` |
| `global.monitoring.pushgatewayUrl` | Pushgateway address, used when `global.monitoring.enabled` is `true`. | `""` |
| `global.ingress.enabled` | Create an Ingress for any component at all. | `true` |
| `global.ingress.className` | Ingress controller class for every Ingress. | `"nginx"` |
| `global.ingress.host` | Base domain. `api.ingress.host`/`ui.ingress.host` default to a subdomain of this when left empty. | `"localtest.me"` |
| `global.ingress.certClusterIssuer` | cert-manager ClusterIssuer that issues each Ingress's TLS certificate. | `"ca-issuer"` |
| `global.ingress.tls` | Terminate TLS at the ingress. Each Ingress declares its own certificate. | `true` |
| `global.trustClusterCa.enabled` | Mount a cluster CA bundle over every container's trust store. All four components call Keycloak or another internal service over HTTPS. | `false` |
| `global.trustClusterCa.configMapName` | ConfigMap holding the bundle. Provided by the cluster, not by this chart. | `overlay-castore` |
| `global.trustClusterCa.key` | Key inside that ConfigMap. Also used as the mount `subPath`. | `ca-certificates.crt` |
| `global.trustClusterCa.mountPath` | File replaced inside the container. Correct for Debian, Ubuntu and Alpine images. | `/etc/ssl/certs/ca-certificates.crt` |

### Submission cross-link parameters

The Agent api validates tokens issued by the Submission product's Keycloak realm, and
calls its API and S3 endpoint. Production points these at the real Submission
deployment.

| **Name** | **Description** | **Value** |
|---|---|---|
| `submission.oidcAuthority` | Full Dare-Control realm URL. Source of `SubmissionKeyCloakSettings__Authority`/`__MetadataAddress`/`__BaseUrl`. | `"http://keycloak/realms/Dare-Control"` |
| `submission.apiUrl` | Public URL of the Submission API. Read into `ApiEndpoints__SubmissionApiUrl`. | `"http://submission-api.localtest.me"` |
| `submission.s3Url` | Submission product's S3 endpoint. Read into `MinioSubSettings__Url`. | `"http://submission-rustfs-svc:9000"` |

### API parameters

| **Name** | **Description** | **Value** |
|---|---|---|
| `api.enabled` | Deploy the API component. | `true` |
| `api.image.repository` | Image for the API. | `harbor.federated-analytics.ac.uk/5s-tes/agent-api` |
| `api.image.tag` | Image tag. Falls back to `global.tag` when empty. | `""` |
| `api.image.pullPolicy` | Image pull policy for the API. | `IfNotPresent` |
| `api.containerPort` | Port the ASP.NET app listens on inside the container. | `8080` |
| `api.resources` | Container resource requests/limits. | `{}` |
| `api.service.type` | API Service type. | `ClusterIP` |
| `api.secretName` | Name of the Kubernetes Secret holding this component's secrets. See **Secrets** above. | `agent-api-secret` |
| `api.ingress.enabled` | Create an Ingress for the API. | `true` |
| `api.ingress.host` | Hostname for the API Ingress. Empty computes `agent-api.<global.ingress.host>`. | `""` |
| `api.rabbitmqHost` | RabbitMQ host address. Read into `RabbitMQ__HostAddress`. | `rabbitmq` |
| `api.oidc.clientId` | Keycloak client ID for the TRE realm. | `Dare-TRE-UI` |
| `api.oidc.validAudiences` | Accepted token audiences for the TRE realm. | `Dare-TRE-API,Dare-TRE-UI` |
| `api.oidc.useRedirectUrl` | Use a fixed redirect URL instead of the request URL. | `"false"` |
| `api.oidc.proxy` | The API sits behind an egress proxy when calling the TRE Keycloak. | `"false"` |
| `api.oidc.proxyAddresUrl` | Proxy address, used when `api.oidc.proxy` is `"true"`. | `""` |
| `api.oidc.bypassProxy` | Comma-separated proxy bypass list. | `""` |
| `api.submissionOidc.clientId` | Keycloak client ID for the Submission realm. | `Dare-Control-API` |
| `api.submissionOidc.validAudiences` | Accepted token audiences for the Submission realm. | `Dare-Control-UI,Dare-Control-API,Dare-Control-Minio` |
| `api.submissionOidc.useRedirectUrl` | Use a fixed redirect URL instead of the request URL. | `"false"` |
| `api.submissionOidc.proxy` | The API sits behind an egress proxy when calling the Submission Keycloak. | `"false"` |
| `api.submissionOidc.proxyAddresUrl` | Proxy address, used when `api.submissionOidc.proxy` is `"true"`. | `""` |
| `api.submissionOidc.bypassProxy` | Comma-separated proxy bypass list. | `""` |
| `api.egress.enabled` | Turn on the Data Egress Keycloak integration. Not every TRE deployment has an Egress layer; the whole `DataEgressKeyCloakSettings__*`/`ApiEndpoints__EgressApiUrl` env group is left unset when `false`. | `false` |
| `api.egress.authority` | Full Data Egress realm URL. Source of `DataEgressKeyCloakSettings__Authority`/`__MetadataAddress`/`__BaseUrl`. | `""` |
| `api.egress.clientId` | Keycloak client ID for the Data Egress realm. | `Data-Egress-API` |
| `api.egress.validAudiences` | Accepted token audiences for the Data Egress realm. | `Data-Egress-UI,Data-Egress-API` |
| `api.egress.tokenExpiredAddress` | Redirect address when an Egress token has expired. | `""` |
| `api.egress.redirectUrl` | OIDC redirect URL override. | `""` |
| `api.egress.useRedirectUrl` | Use `api.egress.redirectUrl` instead of the request URL. | `"false"` |
| `api.egress.proxy` | The API sits behind an egress proxy when calling the Data Egress Keycloak. | `"false"` |
| `api.egress.proxyAddresUrl` | Proxy address, used when `api.egress.proxy` is `"true"`. | `""` |
| `api.egress.bypassProxy` | Comma-separated proxy bypass list. | `""` |
| `api.egress.apiUrl` | URL of the Data Egress API. Read into `ApiEndpoints__EgressApiUrl`. | `""` |
| `api.s3Url` | In-cluster TRE RustFS S3 endpoint. Read into `MinioTRESettings__Url`. | `http://rustfs-svc:9000` |
| `api.publicUrl` | Public URL of the Agent API, embedded in TRE onboarding JSON. Read into `ApiEndpoints__TreApiUrl`. | `http://agent-api.localtest.me` |
| `api.vault.timeoutSeconds` | Vault client timeout. | `30` |
| `api.vault.secretEngine` | Vault secret engine mount. | `secret` |
| `api.vault.enableRetry` | Retry failed Vault calls. | `true` |
| `api.vault.maxRetryAttempts` | Maximum Vault retry attempts. | `3` |
| `api.vault.configPath` | Path under the secret engine where Vault-managed dynamic configuration lives. | `config` |
| `api.vault.configReloadInterval` | Seconds between reloads of that configuration. | `10` |
| `api.zeebe.worker.maxJobsActive` | Maximum Zeebe jobs the worker activates at once. | `5` |
| `api.zeebe.worker.timeoutInMilliseconds` | Job lock timeout. | `5000` |
| `api.zeebe.worker.pollIntervalInMilliseconds` | Interval between job polls. | `1000` |
| `api.zeebe.worker.pollingTimeoutInMilliseconds` | Long-poll timeout per request. | `5000` |
| `api.zeebe.worker.retryTimeoutInMilliseconds` | Retry timeout on a failed job. | `5000` |
| `api.hangfire.enableExternal` | Use the external Hangfire dashboard/storage. | `"true"` |
| `api.jobs.scanSchedule` | Minutes between scans of the Submission layer for available submissions. | `1` |
| `api.jobs.syncSchedule` | Minutes between project/user syncs between TRE and Submission layers. | `10` |
| `api.jobs.healthCheckSchedule` | Minutes between health checks. | `10` |
| `api.jobs.daysBeforeHealthLogDeletion` | Days a health check log is kept before deletion. | `30` |
| `api.useTesk` | Use the TESK backend for task execution. | `"true"` |
| `api.tesApiUrl` | URL of the TES backend. Read into `AgentSettings__TESKAPIURL`. | `http://localhost:8000/v1/tasks` |
| `api.teskOutputBucketPrefix` | Output bucket prefix the TES executing agent writes results to. Read into `AgentSettings__TESKOutputBucketPrefix`. | `s3://` |
| `api.treName` | Name of this TRE deployment. Read into `TreName`. | `DEV` |
| `api.features.seedDemoData` | Seed demo data on startup. | `"false"` |
| `api.features.ephemeralCredentials` | Enable ephemeral credential issuance. | `"true"` |
| `api.keycloakDemoMode` | Allow Keycloak to not require HTTPS. | `"false"` |
| `api.demoModeDefaultP` | Demo mode default password. Unused outside demo mode; kept to prevent startup warnings. | `""` |
| `api.onboarding.isConfigurationImported` | Whether TRE onboarding configuration has already been imported. | `"false"` |
| `api.hasura.enabled` | Enable the Hasura integration. `HasuraSettings__HasuraURL`/`__HasuraAdminSecret` are only read when `true`. | `false` |
| `api.hasura.url` | Hasura URL, used when `api.hasura.enabled` is `true`. | `""` |
| `api.dataProtection.persistKeys` | Persist ASP.NET data-protection keys. Agent apps do not persist these across restarts. | `"false"` |
| `api.dataProtection.keysPath` | Path `DataProtectionSettings__KeysPath` reports; not backed by a volume while `persistKeys` is `"false"`. | `/keys` |
| `api.extraEnv` | Rare one-off environment variables. Anything the app always needs is a named value above instead. | `[]` |

### UI parameters

| **Name** | **Description** | **Value** |
|---|---|---|
| `ui.enabled` | Deploy the UI component. | `true` |
| `ui.image.repository` | Image for the UI. | `harbor.federated-analytics.ac.uk/5s-tes/agent-ui` |
| `ui.image.tag` | Image tag. Falls back to `global.tag` when empty. | `""` |
| `ui.image.pullPolicy` | Image pull policy for the UI. | `IfNotPresent` |
| `ui.containerPort` | Port the ASP.NET app listens on inside the container. | `8080` |
| `ui.resources` | Container resource requests/limits. | `{}` |
| `ui.service.type` | UI Service type. | `ClusterIP` |
| `ui.secretName` | Name of the Kubernetes Secret holding this component's secrets. See **Secrets** above. | `agent-ui-secret` |
| `ui.ingress.enabled` | Create an Ingress for the UI. | `true` |
| `ui.ingress.host` | Hostname for the UI Ingress. Empty computes `agent.<global.ingress.host>`: this is the primary UI. | `""` |
| `ui.appName` | Display name shown in the UI. | `Five Safes TES` |
| `ui.keycloakDemoMode` | Allow Keycloak to not require HTTPS. | `"false"` |
| `ui.sslCookies` | Mark cookies secure. Requires HTTPS end-to-end if `true`. | `"false"` |
| `ui.httpsRedirect` | Redirect HTTP to HTTPS inside the app. Kept `false`; TLS terminates at the ingress. | `"false"` |
| `ui.oidc.clientId` | Keycloak client ID. | `Dare-TRE-UI` |
| `ui.oidc.proxy` | The UI sits behind an egress proxy when calling Keycloak. | `"false"` |
| `ui.oidc.proxyAddresUrl` | Proxy address, used when `ui.oidc.proxy` is `"true"`. | `""` |
| `ui.oidc.bypassProxy` | Comma-separated proxy bypass list. | `""` |
| `ui.helpdeskUrl` | Helpdesk URL shown in the UI. | `https://ukserp.atlassian.net/servicedesk/customer/portal/3` |
| `ui.dataProtection.persistKeys` | Persist ASP.NET data-protection keys. Agent apps do not persist these across restarts. | `"false"` |
| `ui.dataProtection.keysPath` | Path `DataProtectionSettings__KeysPath` reports; not backed by a volume while `persistKeys` is `"false"`. | `/keys` |
| `ui.extraEnv` | Rare one-off environment variables. Anything the app always needs is a named value above instead. | `[]` |

### Camunda parameters

`camunda` has no Service and no Ingress: it is a headless Zeebe job worker that nothing
calls directly.

| **Name** | **Description** | **Value** |
|---|---|---|
| `camunda.enabled` | Deploy the Camunda worker component. | `true` |
| `camunda.image.repository` | Image for the worker. | `harbor.federated-analytics.ac.uk/5s-tes/credentials-camunda` |
| `camunda.image.tag` | Image tag. Falls back to `global.tag` when empty. | `""` |
| `camunda.image.pullPolicy` | Image pull policy for the worker. | `IfNotPresent` |
| `camunda.containerPort` | Port the ASP.NET app listens on inside the container, used by its liveness/readiness probes. | `8080` |
| `camunda.resources` | Container resource requests/limits. | `{}` |
| `camunda.secretName` | Name of the Kubernetes Secret holding this component's secrets. See **Secrets** above. | `credentials-camunda-secret` |
| `camunda.zeebe.worker.maxJobsActive` | Maximum Zeebe jobs the worker activates at once. | `5` |
| `camunda.zeebe.worker.timeoutInMilliseconds` | Job lock timeout. | `5000` |
| `camunda.zeebe.worker.pollIntervalInMilliseconds` | Interval between job polls. | `1000` |
| `camunda.zeebe.worker.pollingTimeoutInMilliseconds` | Long-poll timeout per request. | `5000` |
| `camunda.zeebe.worker.retryTimeoutInMilliseconds` | Retry timeout on a failed job. | `5000` |
| `camunda.ldap.host` | LDAP host. Production overrides this to an external AD. | `openldap` |
| `camunda.ldap.port` | LDAP port. | `389` |
| `camunda.ldap.adminDn` | LDAP admin bind DN. | `cn=admin,dc=camundaephemeral,dc=local` |
| `camunda.ldap.baseDn` | LDAP base DN for user searches. | `dc=camundaephemeral,dc=local` |
| `camunda.ldap.userOu` | Organisational unit holding user entries. | `ou=Users` |
| `camunda.ldap.useSsl` | Use LDAPS. | `false` |
| `camunda.vault.timeoutSeconds` | Vault client timeout. | `30` |
| `camunda.vault.secretEngine` | Vault secret engine mount. | `secret` |
| `camunda.vault.enableRetry` | Retry failed Vault calls. | `true` |
| `camunda.vault.maxRetryAttempts` | Maximum Vault retry attempts. | `3` |
| `camunda.extraEnv` | Rare one-off environment variables. Anything the app always needs is a named value above instead. | `[]` |
