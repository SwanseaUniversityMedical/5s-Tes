# submission-devstack

Dev-only extras for running the Submission stack on a local cluster.
**Never deployed to a shared cluster, never referenced by a tenant deploy repo.**

Install order locally: bootstrap script → this chart → the
[`submission-stack`](../submission-stack/README.md) chart.

## What it provides, and why

Only things production provides another way (doc 09):

| Component | Production provides it as | Here |
|---|---|---|
| Identity provider | Hosted Keycloak `Dare-Control` realm | Bitnami Keycloak `Application` with a hand-written dev realm import and its own bundled dev PostgreSQL |
| App and dependency secrets | `submission-stack`'s `VaultSecret`s | Static `Secret`s under the same names (`postgres-secret`, `submission-api-secret`, `submission-ui-secret`, `submission-rustfs-secret`, `seq-admin-password-secret`) |
| Database GUI | — (ops tooling) | Adminer `Application` |

No mailpit: `EmailSettings.enabled` defaults `"false"` in the standalone chart, so nothing
sends mail locally. To add one later, copy `serp-provisioning-devstack`'s `mailpit.yaml`
pattern and point `email.enabled`/SMTP settings at it.

## Credentials

All local credentials are **fixed, simple strings**, committed on purpose so a recreated
kind cluster keeps the same passwords and nothing in a developer's local configuration has
to change. This is an approved exception to the no-committed-secrets rule (doc 09, rule 1),
and applies only to this chart.

**Each credential is defined exactly once and reused** (doc 09, rule 2): every client
secret in the realm import string-matches the value in `templates/secrets/static.yaml`.

| Credential | Value | Where it's set | Where it's read |
|---|---|---|---|
| PostgreSQL superuser | `postgres` / `password123` | `postgres-secret` | `submission-stack`'s CNPG `Cluster` superuserSecret; also embedded in `submission-api-secret`'s `connectionString` |
| `Dare-Control-API` client secret | `devsecret-control-api` | realm import + `submission-api-secret.keycloakClientSecret` | `SubmissionKeyCloakSettings__ClientSecret` |
| `Dare-Control-UI` client secret | `devsecret-control-ui` | realm import + `submission-ui-secret.keycloakClientSecret` | `SubmissionKeyCloakSettings__ClientSecret` (UI) |
| `Dare-Control-S3` client secret | `devsecret-control-s3` | realm import only | Not yet consumed by any app Secret — the client exists for a future S3-console SSO wire-up; no chart currently reads this value |
| Realm admin user (Keycloak admin REST API) | `dare-control-realm-user` / `admin` | realm import + `submission-api-secret.keycloakAdminUsername`/`keycloakAdminPassword` | `KeycloakAdminService.GetAdminTokenAsync` (password grant against the realm's built-in `admin-cli` client) |
| Dev login user | `dev` / `password123` | realm import only | Manual browser login; holds the `dare-control-admin` realm role so admin-gated pages/endpoints work |
| S3 (RustFS) access/secret key | `s3-submission` / `s3-submission-pass` | `submission-api-secret.s3AccessKey`/`s3SecretKey` + `submission-rustfs-secret.RUSTFS_ACCESS_KEY`/`RUSTFS_SECRET_KEY` | `MinioSettings__AccessKey`/`SecretKey`; RustFS chart's own root credentials |
| Vault token | `dev-only-token` | `submission-api-secret.vaultToken` | `VaultSettings__Token` — unused with `vault.enabled=false` (see **Local install**); no local Vault runs to hold it |
| RabbitMQ default user | `submission` / `password123` | `submission-api-secret.rabbitUsername`/`rabbitPassword` | `RabbitMQ__Username`/`Password` — must match the stack's `rabbitmq.additionalConfig` (see **Local install**) |
| Keycloak admin console | `admin` / `admin` | `keycloak-admin-secret` | Keycloak's own `auth.existingSecret` |
| Seq first-run admin | `admin` / `admin` | `seq-admin-password-secret` | Seq's own `firstRunAdminPasswordSecret` |
| Adminer admin | `admin` / `admin` | `adminer-admin-password` | Adminer's own `auth.existingSecret` |

## Local install

Install this chart first, then `submission-stack` configured to hand off to its static
Secrets and reach the local Keycloak, e.g.:

```
--set global.oidc.authority=http://keycloak.localtest.me/realms/Dare-Control
--set vault.enabled=false
--set rabbitmq.vaultDefaultUser=false
--set-string rabbitmq.additionalConfig="default_user = submission
default_pass = password123
loopback_users.submission = false"
```

- `vault.enabled=false` drops the stack's own Vault `Application` and every `VaultSecret`
  under `templates/secrets/`, so this chart's static Secrets are the only thing producing
  those names/keys.
- `rabbitmq.vaultDefaultUser=false` drops the `RabbitmqCluster`'s `secretBackend.vault`
  block. The `additionalConfig` lines then set the broker's own default user to
  `submission`/`password123` — the same values as this chart's `submission-api-secret`
  (`rabbitUsername`/`rabbitPassword` above), so the app's RabbitMQ credential is defined
  once and reused into the broker, not redefined.
- `global.oidc.authority` must point at the local Keycloak's realm URL; the stack chart's
  own default is the production authority, which does not exist locally.

## What the local cluster must already have

The setup script (`dev-env-setup/`, not yet created in this repo) is expected to install,
before both charts:

- ingress-nginx,
- cert-manager with a self-signed `ClusterIssuer` (`ca-issuer`),
- ArgoCD, watching `Application`s in `5s-tes-submission`, with a matching `AppProject`,
- the CloudNativePG operator,
- the RabbitMQ Cluster Operator.

## Local Keycloak

- URL: `https://keycloak.localtest.me` (admin console), realm `Dare-Control`.
- `submission-stack`'s `global.oidc.authority` must be overridden to reach this Keycloak
  — see **Local install** above.
- The dev realm mirrors the external prod realm's shape (same realm name `Dare-Control`,
  same three client IDs, same `dare-tre-admin` role name so `KeycloakAdmin__ServiceAccountRole`
  behaves identically) but is a hand-written, minimal stand-in: no protocol mappers, no
  audience scopes, `sslRequired: none`, and pure wildcard `redirectUris`/`webOrigins`
  (`["*"]`) — dev shortcuts, never to be copied into a real realm.

## Values

| Value | Description | Default |
|---|---|---|
| `global.namespace` / `global.argoProject` | Must match `submission-stack` | `5s-tes-submission` / `submission` |
| `global.ingress.host` / `className` | Local DNS suffix and ingress class | `localtest.me` / `nginx` |
| `keycloak.*` | Bitnami chart pin + org image mirror | `25.4.0` / `harbor.ukserp.ac.uk` |
| `adminer.*` | Chart pin | `0.1.8` |

## Limits

- The dev realm is a minimal starting point: three clients, two users, two realm roles.
  No protocol mappers or client scopes — `api.oidc.validAudiences`'s `Dare-Control-Minio`
  entry is only an accepted audience string in token validation, not a client; the realm
  needs no client by that name. Extend the realm by editing `templates/keycloak-realm.yaml`.
- This chart is installed from the working tree; it is not published to Harbor and has no
  release workflow.
