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
| Vault token | `dev-only-token` | `submission-api-secret.vaultToken` | `VaultSettings__Token` — see **RabbitMQ and Vault** below |
| RabbitMQ default user | `submission` / `password123` | `submission-api-secret.rabbitUsername`/`rabbitPassword` only | `RabbitMQ__Username`/`Password` — see **RabbitMQ and Vault** below, this does not yet authenticate against a real broker |
| Keycloak admin console | `admin` / `admin` | `keycloak-admin-secret` | Keycloak's own `auth.existingSecret` |
| Seq first-run admin | `admin` / `admin` | `seq-admin-password-secret` | Seq's own `firstRunAdminPasswordSecret` |
| Adminer admin | `admin` / `admin` | `adminer-admin-password` | Adminer's own `auth.existingSecret` |

## RabbitMQ and Vault: a known local-dev gap

`submission-stack`'s `RabbitmqCluster` always sets `secretBackend.vault` (there is no
`vaultDefaultUser`-style toggle here, unlike `serp-provisioning-stack`), so its default
user credentials are never a Kubernetes `Secret` this chart can stand in for — the
RabbitMQ Cluster Operator reads them directly from Vault via its own native integration.
(Checked: per the RabbitMQ Cluster Operator docs, when no `secretBackend` is set at all
the operator instead generates a `<cluster-name>-default-user` Secret — i.e.
`rabbitmq-default-user` here — with `username`/`password` keys. That fallback does not
apply to this stack, because `rabbitmq.yaml` always configures `secretBackend.vault`.)

`submission-stack` also deploys its own app-owned Vault (`templates/vault.yaml`, see that
chart's README), which starts sealed and must be initialised/unsealed by hand — the same
Vault instance is used locally and in production. Once that local Vault is up, seed it at
`{{ .Values.vault.secretPath }}/rabbitmq` (default `kvv2/data/prod/prod/submission/rabbitmq`)
with `rabbit_username: submission` / `rabbit_password: password123` — matching this
chart's `submission-api-secret` values above — plus Vault Kubernetes-auth enabled with a
role named `{{ .Values.vault.role }}` (default `submission`), so the operator can actually
authenticate. None of that Vault bootstrapping is automated by any chart today; until it
is (a future `dev-env-setup/` task, mirroring `serp-provisioning`'s local Argo overrides),
RabbitMQ login will fail on a fresh local install even though the credentials in
`submission-api-secret` are internally consistent.

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
- The standalone chart's default `global.oidc.authority` is `http://keycloak/realms/Dare-Control`
  — the in-cluster Service name `keycloak` this chart's Application creates — so no override
  is needed for a local install.
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
  entry (see `charts/submission/values.yaml`) is a pre-existing app config value with no
  matching client in this realm; audience validation was out of this chart's scope.
  Extend the realm by editing `templates/keycloak-realm.yaml`.
- This chart is installed from the working tree; it is not published to Harbor and has no
  release workflow.
