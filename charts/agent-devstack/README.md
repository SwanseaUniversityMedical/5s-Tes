# agent-devstack

Dev-only extras for running the Agent stack on a local cluster.
**Never deployed to a shared cluster, never referenced by a tenant deploy repo.**

Install order locally: bootstrap script → this chart → the
[`agent-stack`](../agent-stack/README.md) chart.

## What it provides, and why

Only things production provides another way (doc 09):

| Component | Production provides it as | Here |
|---|---|---|
| Identity provider | Hosted Keycloak `Dare-TRE` (and, if `egress.enabled`, `Data-Egress`) realm | Bitnami Keycloak `Application` with hand-written dev realm imports and its own bundled dev PostgreSQL |
| App and dependency secrets | `agent-stack`'s `VaultSecret`s | Static `Secret`s under the same names (`postgres-secret`, `agent-api-secret`, `agent-ui-secret`, `credentials-camunda-secret`, `agent-rustfs-secret`, `seq-admin-password-secret`, `agent-openldap-secret` if `openldap.enabled`, `egress-api-secret`/`egress-ui-secret` if `egress.enabled`) |
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
| `Dare-TRE-API` client secret | `devsecret-tre-api` | realm import + `egress-api-secret.treKeycloakClientSecret` (if `egress.enabled`) | Not directly consumed by any app Secret otherwise — `Dare-TRE-API` is a valid audience for `Dare-TRE-UI` tokens (`api.oidc.validAudiences`), not a client the chart authenticates as. `serviceAccountsEnabled` stays on to mirror the prod realm's client shape, but has no realm role grant: no verified consumer of a client-credentials token exists in this codebase. `egress-api-secret.treKeycloakClientSecret` is the egress api's own view of this same client secret — cross-realm, used to call `agent-api`. If `egress.enabled`, this client also gets a self-`oidc-audience-mapper` (`Dare-TRE-API` in its own tokens' audience) — the egress api authenticates here via password grant as `Dare-TRE-API`, and that token must pass `agent-api`'s `ValidAudiences="Dare-TRE-API,Dare-TRE-UI"` |
| `Dare-TRE-UI` client secret | `devsecret-tre-ui` | realm import + `agent-api-secret.treKeycloakClientSecret`, `agent-ui-secret.keycloakClientSecret` | `TreKeyCloakSettings__ClientSecret` (api, ui) — the single client used by api and ui |
| `Dare-TRE-S3` client secret | `devsecret-tre-s3` | realm import only | Not yet consumed by any app Secret — the client exists for a future S3-console SSO wire-up, same status as `submission-devstack`'s `Dare-Control-S3` |
| Dev login user | `dev` / `password123` | realm import only | Manual browser login; holds the `dare-tre-admin` realm role so `[Authorize(Roles = "dare-tre-admin")]` controllers in `Agent.Api` and `Agent.Web` work. If `egress.enabled`, also holds `data-egress-admin` in this realm — the egress api's cross-realm password-grant call requires it (DARE-Control `TreClientWithoutTokenHelper.cs`'s `requiredRole`), independently of that same-named role in the `Data-Egress` realm below |
| `Dare-Control-API` client secret (cross-realm) | `devsecret-control-api` | `agent-api-secret.submissionKeycloakClientSecret` only — not in this chart's own realm | `SubmissionKeyCloakSettings__ClientSecret` equivalent on the api side. Must equal `submission-devstack`'s own `Dare-Control-API` secret; only matters if the Submission product is also running locally (cross-realm token validation) |
| `Data-Egress-API` client secret | `devsecret-egress-api` (`dev-egress-unused` when `egress.enabled` is `false`) | realm import (if `egress.enabled`) + `agent-api-secret.egressKeycloakClientSecret` + `egress-api-secret.dataEgressKeycloakClientSecret` | Only read when `api.egress.enabled` is `true`; `agent-stack` sets that from `egress.enabled` (default `false`). `agent-api-secret`'s value only switches to the real secret when `egress.enabled` is `true` on this chart too — kept as the inert placeholder otherwise, so the `egress.enabled=false` render stays byte-identical |
| `Data-Egress-UI` client secret | `devsecret-egress-ui` | realm import + `egress-ui-secret.keycloakClientSecret` | Data-Egress-UI's own client secret, read by DARE-Control's `Data-Egress-UI` app into its `DataEgressKeyCloakSettings` (`src/Data-Egress-UI/Program.cs:52-53`) — not read by any Secret in this chart's own apps. Whether the UI ever actually presents this secret (vs. authenticating as `Data-Egress-API`, which compose configures as the UI's own `DataEgressKeyCloakSettings__ClientId`) is open — see the audit's Open Question 1 |
| Data-Egress dev login user | `dev` / `password123` | realm import only | Manual browser login to `Data-Egress-UI`; holds realm roles `dare-tre-admin` and `data-egress-admin`. `dare-tre-admin` — same role name as the `Dare-TRE` realm's role above, but an independent role in the `Data-Egress` realm — is required by the ROPC token request `Agent.Api`'s `DataEgressClientWithoutTokenHelper` makes against this realm (`Agent/Agent.Api/Services/DataEgressClientWithoutTokenHelper.cs:27`, role check in `Shared/FiveSafesTes.Core/Services/KeycloakCommon.cs`); that flow itself authenticates with a username/password stored in the app's own `KeycloakCredentials` DB table (application data, not this chart), not this realm-import user directly. `data-egress-admin` gates nearly every controller in DARE-Control's `Data-Egress-API`/`Data-Egress-UI` (`[Authorize(Roles = "data-egress-admin")]`, 23 hits) — without it the dev user cannot open the Data-Egress UI's home page |
| Data Egress connection string | `Server=pg-pooler;Port=5432;Database=DATA-Egress;User Id=postgres;Password=password123;TrustServerCertificate=True;` | `egress-api-secret.connectionString` | Same shape as `agent-api-secret`'s connection strings, against `DATA-Egress` (`agent-stack`'s `postgres.egressDatabase`) |
| Data Egress AES key/IV | `ZGV2ZWdyZXNza2V5MTYhIQ==` / `ZGV2ZWdyZXNzYmFzZTE2IQ==` (base64, 16 bytes each) | `egress-api-secret.encryptionKey`/`encryptionBase` | AES-128 key/IV decrypting `KeycloakCredentials.PasswordEnc` rows in the `DATA-Egress` DB; fixed dev values, distinct from any real deployment's — changing them after data exists makes those rows undecryptable |
| Data Egress demo seed password | `password123` | `egress-api-secret.demoModeDefaultPassword` | Seeded verbatim as the Keycloak password for two demo service-account credential rows when the egress api's own `DemoMode` is on |
| S3 (RustFS) access/secret key | `s3-tre` / `s3-tre-pass` | `agent-api-secret.s3AccessKey`/`s3SecretKey` + `agent-rustfs-secret.RUSTFS_ACCESS_KEY`/`RUSTFS_SECRET_KEY` + `egress-api-secret.s3AccessKey`/`s3SecretKey` (if `egress.enabled`) | RustFS chart's own root credentials; api's and egress api's S3 client — egress uses the same TRE object store, no separate bucket/credentials |
| Vault token | `dev-only-token` | `agent-api-secret.vaultToken` + `credentials-camunda-secret.vaultToken` | `VaultSettings__Token` on both api and the Credentials Camunda worker — the local Vault runs (`vault.enabled` stays `true`; see **Local install**) and must be configured with a token equal to this value after init/unseal, or these Secrets' values updated to match the real token |
| RabbitMQ default user | `agent` / `password123` | `agent-api-secret.rabbitUsername`/`rabbitPassword` | `RabbitMQ__Username`/`Password` — must match the stack's `rabbitmq.additionalConfig` (see **Local install**) |
| Encryption key | `ZGV2LWFnZW50LWVuY3J5cHRpb24ta2V5LTMyYnl0ZSE=` (base64, 32 bytes) | `agent-api-secret.encryptionKey` | The api's encryption key setting |
| Hangfire dashboard | `admin` / `password123` | `agent-api-secret.hangfireUsername`/`hangfirePassword` | Hangfire basic auth |
| Hasura admin secret | `dev-hasura-admin-secret-unused` | `agent-api-secret.hasuraAdminSecret` | Only read when `api.hasura.enabled` is `true` (not surfaced by `agent-stack`, default `false`) |
| LDAP admin bind password | `admin` | `credentials-camunda-secret.ldapAdminPassword` + `agent-openldap-secret.LDAP_ADMIN_PASSWORD`/`LDAP_CONFIG_ADMIN_PASSWORD` (if `openldap.enabled`) | Credentials Camunda worker's directory bind; OpenLDAP's own admin/config-admin passwords. Matches the stack's `openldap.yaml` chart contract |
| tredata (TRE data DB stand-in) | `postgres` / `password123`, database `tredata` | `tredata.yaml`'s own `auth.postgresPassword`/`auth.database` | `credentials-camunda-secret.connectionStringTreData`, pointed at the `tredata-postgresql` Service (Bitnami chart's own naming, release name `tredata`) |
| Keycloak admin console | `admin` / `admin` | `keycloak-admin-secret` | Keycloak's own `auth.existingSecret` |
| Seq first-run admin | `admin` / `admin` | `seq-admin-password-secret` | Seq's own `firstRunAdminPasswordSecret` |
| Adminer admin | `admin` / `admin` | `adminer-admin-password` | Adminer's own `auth.existingSecret` |

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
```

`dev-env-setup/files/values/agent-devstack-local.yaml` and `agent-stack-local.yaml` carry
this set (plus the per-family host suffix below) for the two-family bootstrap; this recipe
stays canonical for running Agent alone.

**Running Agent alongside Submission on one cluster** (what `dev-env-setup/cluster-setup.sh`
does): both devstack charts render a Keycloak Ingress at `keycloak.<global.ingress.host>` and
an Adminer Ingress at `adminer.<global.ingress.host>`, and both stack charts render Seq/RustFS
the same way - one shared `localtest.me` host means ingress-nginx keeps only one family's
Ingress per host and silently drops the other. Give each family its own suffix instead (still
under the `*.localtest.me` wildcard, which resolves any subdomain to `127.0.0.1`):
`global.ingress.host=agent.localtest.me` (and `keycloak.submission.localtest.me` for
Submission's own realm URL, not this one). See `dev-env-setup/README.md`.

**Fallback only**: on a cluster without `dev-env-setup`'s RWX provisioner patch (step 2 of
`cluster-setup.sh`), add `--set 'agent.processModels.accessModes[0]=ReadWriteOnce'` - the
stack's own default (`[ReadWriteMany]`) will not bind against the plain kind
`local-path-provisioner`.

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
- The local Vault still starts sealed and needs init/unseal:
  `dev-env-setup/vault-init.sh` does this (init, unseal, enable the `secret` mount, and
  create a `dev-only-token` root-policy token matching these Secrets' `vaultToken` — see its
  README section). See `agent-stack`'s README **Vault** section for the manual/prod steps
  this mirrors.
- `rabbitmq.vaultDefaultUser=false` drops the `RabbitmqCluster`'s `secretBackend.vault`
  block. The `additionalConfig` lines then set the broker's own default user to
  `agent`/`password123` — the same values as this chart's `agent-api-secret`
  (`rabbitUsername`/`rabbitPassword` above), so the app's RabbitMQ credential is defined
  once and reused into the broker, not redefined.

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

### Optional: local Data Egress

```
--set egress.enabled=true    # on THIS chart, so the Data-Egress realm import and
                             # egress-api-secret/egress-ui-secret render
```

and, on `agent-stack`:

```
--set egress.enabled=true
--set egress.oidcAuthority=http://keycloak.localtest.me/realms/Data-Egress
--set egress.keycloakDemoMode=true
--set agent.api.keycloakDemoMode=true
```

The extra `egress.oidcAuthority` override is needed for the same reason as `global.oidc.authority`
above: `agent-stack`'s own default is the production Data-Egress authority, which does not exist
locally. Both toggles must agree — `agent-stack`'s `egress.enabled` renders the `egress`
`Application`, the `data-egress` `Database`, and the `egress-*` `VaultSecret`s (or reads this
chart's static Secrets directly if `vault.secretsEnabled=false`, same as the rest of this recipe);
this chart's `egress.enabled` renders the realm clients and matching static Secrets those need.
Mismatched toggles fail differently depending on direction: this chart's `egress.enabled=false`
with `agent-stack`'s `true` leaves `agent-api-secret.egressKeycloakClientSecret` at the inert
`dev-egress-unused` placeholder (see **Credentials** above) — the ROPC call fails with a bad
client secret, not an obviously missing object. `agent-stack`'s own README states the egress
chart (`harbor.ukserp.ac.uk/dare-trefx/chart/egress`) must already exist in Harbor before its
`egress.enabled` is turned on — see that chart's **Egress** section.

`egress.keycloakDemoMode`/`agent.api.keycloakDemoMode` (both default `"false"`, production-safe)
relax the outbound password-grant token helpers' discovery-endpoint check from HTTPS to HTTP
(`KeycloakCommon.cs`'s `RequireHttps = !keycloakDemoMode`, both DARE-Control's and this repo's
copy of that file) — required because `http://keycloak.localtest.me` above is plain HTTP.
`egress.keycloakDemoMode` covers the egress api's own outbound calls (to `Data-Egress`'s own
discovery endpoint, and, via the same value, to `Dare-TRE` as `Dare-TRE-API` — DARE-Control
`Data-Egress-API/Program.cs:69,77` sets both `TreKeyCloakSettings` and `DataEgressKeyCloakSettings`
from one `KeycloakDemoMode` env var). `agent.api.keycloakDemoMode` covers `agent-api`'s own
outbound call into `Data-Egress` (`DataEgressClientWithoutTokenHelper.cs`). Without both set,
outbound token requests reject the local HTTP discovery document and these flows fail even though
the realm and role/audience setup above is otherwise correct.

### GA4GH TES backend

`agent-stack`'s own dev default for `agent.api.tesApiUrl` (`http://localhost:8000/v1/tasks`) is
a broken loopback address once actually in-cluster. **For local TES testing, use
`director-wfs.sh`** (the org's director-wfs repository) to bring up a disposable kind-based
TESK environment, then set `agent.api.tesApiUrl` on `agent-stack` to its `tesk-api` Service —
do not enable `agent-stack`'s own `tesk.enabled` for this purpose (see its README, **GA4GH TES
backend**). A local GA4GH Funnel instance is an equally valid substitute if you have one.

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

- URL: `http://keycloak.localtest.me` (admin console), realm `Dare-TRE` (and, if
  `egress.enabled`, realm `Data-Egress`). Plain HTTP: `templates/keycloak.yaml`'s `ingress`
  block sets no `tls` key, and the chart's default is `false`.
- `agent-stack`'s `global.oidc.authority` (and, if egress is enabled, `egress.oidcAuthority`)
  must be overridden to reach this Keycloak — see **Local install** above.
- The dev realms mirror the external prod realms' shape (same realm names, same client IDs)
  but are hand-written, minimal stand-ins: `sslRequired: none` and pure wildcard
  `redirectUris`/`webOrigins` (`["*"]`) are dev shortcuts, never to be copied into a real
  realm. `Dare-TRE-UI` always carries an `oidc-audience-mapper` adding `Dare-TRE-API` to its
  tokens' audience, mirroring production's `DARE-TRE-API` client scope
  (`DemoStack/config/realm-config/tre-layer.json`, `clientScopes[].name == "DARE-TRE-API"`).
  If `egress.enabled`, `Dare-TRE-API` gets a matching self-mapper too (same production shape:
  prod's export puts both `DARE-TRE-UI` and `DARE-TRE-API` default client scopes on **both**
  `Dare-TRE` clients, `DeploymentStack/TRE/config/realm-config/tre-layer.json`) — needed because
  the egress api's cross-realm password-grant token is issued *as* `Dare-TRE-API`, and it must
  independently pass `agent-api`'s `ValidAudiences` check. `Data-Egress-API` and `Data-Egress-UI`
  both carry two `oidc-audience-mapper`s (adding `Data-Egress-UI` and `Data-Egress-API` to every
  token's audience, regardless of which of the two clients issued it) — DARE-Control's
  `Data-Egress-API` validates `ValidAudiences="Data-Egress-UI,Data-Egress-API"` against the token
  the UI forwards verbatim, mirroring production's `DATA-EGRESS-UI`/`DATA-EGRESS-API` client
  scopes, which are `defaultClientScopes` on **both** clients in the prod export.
- **Known local constraint, now closed by the bootstrap**: server-side OIDC calls made from
  inside pods (api and ui reaching `global.oidc.authority`) would otherwise resolve
  `keycloak.<global.ingress.host>` to `127.0.0.1`, not the ingress controller —
  `*.localtest.me` is a wildcard domain that always resolves to loopback.
  `dev-env-setup/cluster-setup.sh` rewrites CoreDNS (`files/deps/coredns.yaml`) so both
  families' Keycloak hosts resolve in-cluster to the ingress controller Service instead.
  Installing this chart outside that bootstrap still needs the equivalent mapping done
  manually (cluster DNS rewrite, or `/etc/hosts` on every node).
- The standalone chart still appends `/.well-known/openid-configuration` to this authority for
  `*KeyCloakSettings__Authority` — matching compose, deliberate: issuer validation is satisfied
  by the OIDC metadata's fetched `Issuer` field, not a literal match against `Authority`
  (`Agent.Api/Program.cs:196-198,237,241`).

## Host access for development

`templates/dev-access.yaml` (gated on `devAccess.enabled`, default `true`) creates parallel
`dev-*` NodePort Services carrying `agent-stack`'s own Service selectors, so a natively-running
app (e.g. VS Code) reaches every dependency at `localhost:<port>` without port-forwarding.
`agent-stack`'s own Services are never patched. Fixed nodePorts, matched by
`dev-env-setup/kind-config.yaml`'s `extraPortMappings` (changing that file needs a cluster
recreation):

| Service | Dependency | Container port | nodePort / host port |
|---|---|---|---|
| `dev-pg-pooler` | `pg-pooler` (pgbouncer) | 5432 | 31432 |
| `dev-rabbitmq` | `rabbitmq` (amqp) | 5672 | 30673 |
| `dev-rabbitmq` | `rabbitmq` (management) | 15672 | 31673 |
| `dev-rustfs` | `rustfs-svc` (endpoint) | 9000 | 30902 |
| `dev-seq` | `seq` (ingestion) | 5341 | 31341 |
| `dev-vault` | `agent-vault` (http) | 8200 | 31200 |
| `dev-camunda-zeebe-gateway` | `camunda-zeebe-gateway` (grpc) | 26500 | 30500 |
| `dev-openldap` | the OpenLDAP chart's `openldap` Service (ldap-port) | 389 | 30389 |
| `dev-tredata` | `tredata-postgresql` (tcp-postgresql) | 5432 | 31433 |

`dev-openldap` always renders — its selector (`app.kubernetes.io/component: openldap`,
`release: openldap`, matching the `jp-gouin/helm-openldap` chart at release name `openldap`)
only matches pods once `agent-stack`'s own `openldap.enabled` is also `true` (see **Optional:
local OpenLDAP** above); with it off, the Service simply has no endpoints. No console port for
RustFS here (unlike `submission-devstack`'s `dev-rustfs`) — the console has no NodePort by
design, it's already ingress-reachable at `rustfs.<global.ingress.host>`.
`dev-tredata` is the one exception to "carrying `agent-stack`'s own Service selectors" above —
`tredata-postgresql` is this chart's (`agent-devstack`'s) own `tredata` Application, the
dev stand-in for the external TRE data database, not one of `agent-stack`'s dependencies.

Keycloak and other web UIs need no NodePort — ingress plus `*.localtest.me` already reach them
from the host.

## Values

| Value | Description | Default |
|---|---|---|
| `global.namespace` / `global.argoProject` | Must match `agent-stack` | `5s-tes-agent` / `agent` |
| `global.ingress.host` / `className` | Local DNS suffix and ingress class | `localtest.me` / `nginx` |
| `keycloak.*` | Bitnami chart pin + org image mirror | `25.4.0` / `harbor.ukserp.ac.uk` |
| `adminer.*` | Chart pin | `0.1.8` |
| `tredata.*` | Bitnami PostgreSQL chart pin + org image mirror, dev stand-in for the external TRE data database | `16.7.21` / `harbor.ukserp.ac.uk` |
| `openldap.enabled` | Render `agent-openldap-secret`. Set alongside `agent-stack`'s own `openldap.enabled` — see **Optional: local OpenLDAP** | `false` |
| `egress.enabled` | Render the `Data-Egress` realm import and `egress-api-secret`/`egress-ui-secret`. Set alongside `agent-stack`'s own `egress.enabled` — see **Optional: local Data Egress** | `false` |
| `devAccess.enabled` | Render the `dev-*` NodePort Services, see **Host access for development** | `true` |

## Limits

- The `Dare-TRE` dev realm is a minimal starting point: three clients, two users (including
  the `Dare-TRE-API` service account), one realm role, one protocol mapper (the `Dare-TRE-UI`
  audience mapper, see **Local Keycloak** above) — two realm roles and one extra protocol mapper
  (on `Dare-TRE-API`) if `egress.enabled`. No other client scopes. The `Data-Egress` dev realm
  (if `egress.enabled`) is equally minimal: two clients, three users (including both service
  accounts), two realm roles (`dare-tre-admin`, `data-egress-admin` — see **Credentials** above),
  two protocol mappers per client (both audience mappers, see **Local Keycloak** above). Extend
  either realm by editing `templates/keycloak-realm.yaml`.
- Keycloak only imports a realm on first start; it skips one that already exists by name. To
  apply an EDIT to an already-imported realm (e.g. changing `Dare-TRE`'s existing client
  secrets or roles) on an already-running local Keycloak, delete the `keycloak` Application's
  PostgreSQL PVC (or delete the realm via the admin console) and let ArgoCD re-sync — the more
  drastic step is needed because Keycloak will otherwise silently skip re-importing a realm it
  already has. Turning on `egress.enabled` on an already-running Keycloak is an EDIT of this
  kind, not just a new realm: it adds `Data-Egress` AND changes the existing `Dare-TRE` realm
  file (the `data-egress-admin` role, its `dev`-user grant, and the `Dare-TRE-API` audience
  mapper). A `rollout restart` alone imports only the never-seen `Data-Egress` and silently
  skips the changed `Dare-TRE`, so the cross-realm egress→agent flow stays broken — use the
  full procedure above (delete the `keycloak` Application's PostgreSQL PVC, or delete both
  realms via the admin console, and let ArgoCD re-sync) so both files import fresh.
- This chart is never published to Harbor; installs use the working tree.
- The postgresql chart's own default `image.tag` (`17.5.0-debian-12-r20`) is not mirrored
  at `harbor.ukserp.ac.uk/bitnami/postgresql` (found by local boot: kubelet
  `ImagePullBackOff`, "not found"). `tredata.imageTag` pins `image.tag` to a tag
  confirmed present on that mirror instead (anonymous pull verified).
