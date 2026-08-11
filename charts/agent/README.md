# agent

Standalone chart for the 5S TES Agent (TRE layer).

## What this chart deploys

- `api` — Agent.Api, the TRE-side API. Also runs the Hangfire sync/scan/health jobs and consumes RabbitMQ messages.
- `ui` — Agent.Web, the MVC frontend for the API.
- `web` — agent-web, the Next.js frontend for the API.
- `camunda-worker` — Credentials.Camunda, the Zeebe worker that issues ephemeral credentials.

## What must already exist

- The Secrets listed below, in the release namespace.
- A ConfigMap holding the cluster CA bundle (see `trustClusterCa`).
- A ReadWriteMany-capable storage class for the shared process models claim.

## What this chart does not do

It does not install PostgreSQL, Keycloak, RabbitMQ, Vault, Seq, Camunda,
OpenLDAP, the S3 object stores, the Data Egress services or a TES executor
(TESK/Funnel). It expects their addresses as values. The `agent-stack` chart
installs the in-cluster ones and points this chart at them.

## Secrets

This chart does **not** create these. They must already exist in the
namespace before the chart is installed. In our deployments the
`agent-stack` chart creates them from Vault.

### `agent-api-secret`

Set by `api.secretName`.

| **Key** | **Used for** | **Required** |
|---|---|---|
| `connectionString` | Full PostgreSQL connection string for the `DARE-Tre` database, including the password. Read into `ConnectionStrings__DefaultConnection`. Hangfire also stores its jobs here. | Yes |
| `credentialsConnectionString` | Connection string for the `TRE_Credentials` database. | Yes |
| `encryptionKey` | Base64 AES key (16, 24 or 32 bytes) for `EncryptionSettings__Key`. The API refuses to start without it. | Yes |
| `treKeycloakClientSecret` | Client secret for the `Dare-TRE-UI` Keycloak client. | Yes |
| `egressKeycloakClientSecret` | Client secret for the `Data-Egress-API` Keycloak client. | Yes |
| `submissionKeycloakClientSecret` | Client secret for the `Dare-Control-API` Keycloak client on the Submission side. | Yes |
| `rabbitPassword` | Password for the RabbitMQ user named in `global.config.rabbitmq.username`. | Yes |
| `s3TreAccessKey` | Access key for the TRE object store at `api.s3Tre.url`. | Yes |
| `s3TreSecretKey` | Secret key matching `s3TreAccessKey`. | Yes |
| `vaultToken` | Token for the Vault at `global.config.vault.url`. The API also reloads configuration from Vault path `config`. | Yes |
| `hangfirePassword` | Password for the Hangfire dashboard at `/hangfire`. Pairs with `api.hangfire.username`. | Yes |

### `agent-ui-secret`

Set by `ui.secretName`.

| **Key** | **Used for** | **Required** |
|---|---|---|
| `keycloakClientSecret` | Client secret for the `Dare-TRE-UI` Keycloak client. | Yes |

### `agent-web-secret`

Set by `web.secretName`.

| **Key** | **Used for** | **Required** |
|---|---|---|
| `keycloakClientSecret` | Client secret for the `Dare-TRE-UI` Keycloak client. | Yes |
| `betterAuthSecret` | Session signing secret for better-auth. Any long random string; changing it signs everyone out. | Yes |

### `agent-camunda-worker-secret`

Set by `camundaWorker.secretName`.

| **Key** | **Used for** | **Required** |
|---|---|---|
| `credentialsConnectionString` | Connection string for the `TRE_Credentials` database. | Yes |
| `treDataConnectionString` | Connection string for the research data PostgreSQL that ephemeral credentials are granted against. A site-specific database, not the platform one. | Yes |
| `ldapAdminPassword` | Admin password for the OpenLDAP at `camundaWorker.ldap.host`. | Yes |
| `vaultToken` | Token for the Vault at `global.config.vault.url`, where issued credentials are stored. | Yes |

## Parameters

### Common parameters

| **Name** | **Description** | **Value** |
|---|---|---|
| `nameOverride` | Replaces the chart name in object names. | `""` |
| `fullnameOverride` | Replaces the full prefix on every object name. The stack chart sets this so service DNS names stay predictable. | `"agent"` |
| `imagePullSecrets` | Secrets used to pull images from a private registry. | `[]` |
| `serviceAccount.create` | Create a service account for the pods. | `true` |
| `serviceAccount.annotations` | Annotations for the service account. | `{}` |
| `serviceAccount.name` | Use an existing service account name instead. | `""` |
| `podSecurityContext.fsGroup` | Group that owns mounted volumes, so the non-root app user can write the shared process models. | `1000` |
| `securityContext.*` | Organisation security baseline (non-root, read-only filesystem, no capabilities). Do not weaken without a reason in the pull request. | see values |

### Certificate trust

| **Name** | **Description** | **Value** |
|---|---|---|
| `trustClusterCa.enabled` | Mount a cluster CA bundle over the container trust store. Turn off only on a cluster that has no such bundle. | `true` |
| `trustClusterCa.configMapName` | ConfigMap holding the bundle. Provided by the cluster, not by this chart. | `overlay-castore` |
| `trustClusterCa.key` | Key inside that ConfigMap. Also used as the mount `subPath`. | `ca-certificates.crt` |
| `trustClusterCa.mountPath` | File replaced inside the container. Correct for Debian, Ubuntu and Alpine images. Also handed to Node via `NODE_EXTRA_CA_CERTS` in the `web` component. | `/etc/ssl/certs/ca-certificates.crt` |

### Storage

| **Name** | **Description** | **Value** |
|---|---|---|
| `persistentVolumeLabels` | Labels put on every PersistentVolumeClaim, for the cluster backup tool. Set `{}` for none. | `hiru.io/backup: "enabled"` |
| `processModelsStorage.size` | Size of the shared DMN process models claim. | `1Gi` |
| `processModelsStorage.storageClassName` | Storage class for that claim. Must support ReadWriteMany. `null` uses the cluster default. | `null` |
| `processModelsStorage.mountPath` | Where the models are mounted in the API and worker. Also sets `DmnPath__Path`. | `/app/ProcessModels` |

### Global parameters

| **Name** | **Description** | **Value** |
|---|---|---|
| `global.tag` | Image tag used by any component that does not pin its own. | `latest` |
| `global.ingress.enabled` | Master switch for every Ingress in the chart. | `true` |
| `global.ingress.className` | Ingress controller class. | `nginx` |
| `global.ingress.certClusterIssuer` | cert-manager ClusterIssuer that issues the TLS certificates. | `ca-issuer` |
| `global.ingress.tls` | Render the TLS blocks. Turn off for local clusters with no issuer. | `true` |
| `global.config.seqUrl` | Address of the Seq instance every component logs to. | `http://seq:5341` |
| `global.config.logLevel` | Serilog minimum level for every component. | `Information` |
| `global.config.treKeycloakUrl` | Base URL of the Keycloak holding the `Dare-TRE` and `Data-Egress` realms. Wrong value means nobody can log in. | `http://keycloak.localtest.me:8085` |
| `global.config.submissionKeycloakUrl` | Base URL of the Keycloak holding the `Dare-Control` realm, on the Submission side. | `http://keycloak.localtest.me:8085` |
| `global.config.keycloakDemoMode` | Keycloak demo mode flag. Keep off outside demos. | `false` |
| `global.config.treApiPublicUrl` | Public URL of the agent API. The UIs and onboarding data embed it; must match the API ingress host. | `http://agent-api.localtest.me` |
| `global.config.rabbitmq.host` | RabbitMQ host. Password comes from the API secret. | `rabbitmq` |
| `global.config.rabbitmq.username` | RabbitMQ username. | `rabbitmq` |
| `global.config.vault.url` | Address of the Vault used for credentials and live configuration. | `http://vault:8200` |
| `global.config.zeebeGatewayAddress` | Zeebe gRPC gateway of the Camunda the API and worker connect to. | `orchestration:26500` |
| `global.config.proxy.enabled` | Route Keycloak HTTP calls through a proxy. | `false` |
| `global.config.proxy.url` | Proxy address, when enabled. | `""` |
| `global.config.proxy.bypass` | Comma-separated hosts that skip the proxy. | `agent-api,seq` |

### API parameters

| **Name** | **Description** | **Value** |
|---|---|---|
| `api.enabled` | Deploy the API component. | `true` |
| `api.image.repository` | Image for the API. | `harbor.federated-analytics.ac.uk/5s-tes/agent-api` |
| `api.image.tag` | Image tag. Falls back to `global.tag` when empty. | `""` |
| `api.image.pullPolicy` | Image pull policy. | `IfNotPresent` |
| `api.replicas` | Number of copies. Keep at 1: the recurring jobs and the shared claim are not written for more. | `1` |
| `api.containerPort` | Port the ASP.NET app listens on. Describes the app; it does not change it. | `8080` |
| `api.resources` | CPU and memory requests/limits. | `{}` |
| `api.service.type` | Service type. | `ClusterIP` |
| `api.secretName` | Name of the Kubernetes Secret holding the API secrets. See **Secrets**. | `agent-api-secret` |
| `api.ingress.enabled` | Create an Ingress for the API. | `true` |
| `api.ingress.host` | Hostname for that Ingress. | `agent-api.localtest.me` |
| `api.submissionApiUrl` | Address of the remote Submission API this TRE serves. External URL unless both layers share a cluster. | `http://submission-api.localtest.me` |
| `api.treName` | Name this TRE registers with the Submission layer under. | `SAIL` |
| `api.egress.apiAddress` | Address of the Data Egress API. | `http://egress-api` |
| `api.tes.useTesk` | Submit tasks to a TES executor. Keep true in real deployments. | `true` |
| `api.tes.apiUrl` | TES task endpoint (TESK or Funnel). Tasks go nowhere if this is wrong. | `http://tesk.localtest.me/v1/tasks` |
| `api.tes.outputBucketPrefix` | Prefix put on output bucket paths handed to the executor. | `s3://` |
| `api.jobs.syncSchedule` | Minutes between project/membership syncs. | `10` |
| `api.jobs.scanSchedule` | Minutes between submission scans. | `10` |
| `api.jobs.healthCheckSchedule` | Minutes between health check runs. | `1` |
| `api.jobs.daysBeforeHealthLogDeletion` | Days health check rows are kept. | `30` |
| `api.hangfire.enableExternal` | Expose the Hangfire dashboard at `/hangfire`. | `true` |
| `api.hangfire.username` | Hangfire dashboard username. Password comes from the API secret. | `admin` |
| `api.s3Tre.url` | S3 API of the TRE object store. | `http://rustfs-tre:9002` |
| `api.s3Tre.adminConsole` | Console of that store. | `http://rustfs-tre:9003` |
| `api.s3Sub.url` | S3 API of the Submission layer's object store; external in a split deployment. | `http://rustfs-submission.localtest.me` |
| `api.credentialWebhooks.start` | Camunda connectors inbound webhook that starts credential issue. | `http://connectors:8080/inbound/StartCredentials` |
| `api.credentialWebhooks.revoke` | Webhook that revokes credentials. | `http://connectors:8080/inbound/RevokeCredentials` |
| `api.features.seedDemoData` | Seed demo data on start. Demos only. | `false` |
| `api.features.ephemeralCredentials` | Issue ephemeral database credentials through Camunda. | `true` |
| `api.onboardingConfigImported` | Marks the TRE onboarding configuration as already imported. | `false` |

### UI parameters

| **Name** | **Description** | **Value** |
|---|---|---|
| `ui.enabled` | Deploy the MVC UI component. | `true` |
| `ui.image.repository` | Image for the UI. | `harbor.federated-analytics.ac.uk/5s-tes/agent-ui` |
| `ui.image.tag` | Image tag. Falls back to `global.tag` when empty. | `""` |
| `ui.image.pullPolicy` | Image pull policy. | `IfNotPresent` |
| `ui.replicas` | Number of copies. | `1` |
| `ui.containerPort` | Port the ASP.NET app listens on. | `8080` |
| `ui.resources` | CPU and memory requests/limits. | `{}` |
| `ui.service.type` | Service type. | `ClusterIP` |
| `ui.secretName` | Name of the Kubernetes Secret holding the UI secrets. See **Secrets**. | `agent-ui-secret` |
| `ui.ingress.enabled` | Create an Ingress for the UI. | `true` |
| `ui.ingress.host` | Hostname for that Ingress. | `agent.localtest.me` |
| `ui.apiAddress` | Address the UI calls the API on. The in-chart service name works unless the API is elsewhere. | `http://agent-api` |
| `ui.uiName` | Product name shown in the UI. | `Five Safes TES` |
| `ui.sslCookies` | Mark auth cookies secure. Needs HTTPS end to end. | `false` |
| `ui.httpsRedirect` | Redirect HTTP to HTTPS inside the app. Usually off behind an ingress that already does this. | `false` |

### Web parameters

| **Name** | **Description** | **Value** |
|---|---|---|
| `web.enabled` | Deploy the Next.js web component. | `true` |
| `web.image.repository` | Image for the web frontend. | `harbor.federated-analytics.ac.uk/5s-tes/agent-web` |
| `web.image.tag` | Image tag. Falls back to `global.tag` when empty. | `""` |
| `web.image.pullPolicy` | Image pull policy. | `IfNotPresent` |
| `web.replicas` | Number of copies. Keep at 1 unless the better-auth session store is external. | `1` |
| `web.containerPort` | Port the Node server listens on. | `3000` |
| `web.resources` | CPU and memory requests/limits. | `{}` |
| `web.service.type` | Service type. | `ClusterIP` |
| `web.secretName` | Name of the Kubernetes Secret holding the web secrets. See **Secrets**. | `agent-web-secret` |
| `web.ingress.enabled` | Create an Ingress for the web frontend. | `true` |
| `web.ingress.host` | Hostname for that Ingress. Must match `web.publicUrl`. | `agent-web.localtest.me` |
| `web.apiAddress` | Address the web frontend calls the API on. | `http://agent-api` |
| `web.publicUrl` | Public URL of this frontend; better-auth builds login callbacks from it. | `http://agent-web.localtest.me` |
| `web.helpdeskUrl` | Helpdesk link shown to users. | `https://ukserp.atlassian.net/servicedesk/customer/portal/3` |

### Camunda worker parameters

| **Name** | **Description** | **Value** |
|---|---|---|
| `camundaWorker.enabled` | Deploy the credentials worker. | `true` |
| `camundaWorker.image.repository` | Image for the worker. | `harbor.federated-analytics.ac.uk/5s-tes/credentials-camunda` |
| `camundaWorker.image.tag` | Image tag. Falls back to `global.tag` when empty. | `""` |
| `camundaWorker.image.pullPolicy` | Image pull policy. | `IfNotPresent` |
| `camundaWorker.replicas` | Number of copies. Keep at 1: it shares the process models claim with the API. | `1` |
| `camundaWorker.resources` | CPU and memory requests/limits. | `{}` |
| `camundaWorker.secretName` | Name of the Kubernetes Secret holding the worker secrets. See **Secrets**. | `agent-camunda-worker-secret` |
| `camundaWorker.ldap.host` | OpenLDAP host ephemeral accounts are created in. | `openldap` |
| `camundaWorker.ldap.port` | LDAP port. | `389` |
| `camundaWorker.ldap.adminDn` | Bind DN for the LDAP admin. Password comes from the worker secret. | `cn=admin,dc=camundaephemeral,dc=local` |
| `camundaWorker.ldap.baseDn` | Base DN accounts are created under. | `dc=camundaephemeral,dc=local` |
| `camundaWorker.ldap.userOu` | OU that holds the ephemeral users. | `ou=Users` |
| `camundaWorker.ldap.useSsl` | Use LDAPS. | `false` |
