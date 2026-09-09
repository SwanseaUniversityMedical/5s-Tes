![ Five Safes TES logo][5s-tes-logo]

Five Safes TES supports the secure, remote execution of [GA4GH TES](https://ga4gh.github.io/task-execution-schemas/docs/) analyses in Trusted Research Environments (TREs).

- Provides a standardised API for job submission and monitoring
- Enables the execution of GA4GH TES tasks inside TREs
- Supports federated analysis

Deployment repository for the stack
is : [5S-TES-deployment](https://github.com/SwanseaUniversityMedical/5S-TES-deployment)

![.NET][net-badge][![Release][release-badge]][release][![Five Safes TES docs][docs-badge]][5s-tes-docs]

## Submission

- Provides an API and user interface for researchers to submit tasks.
- Authenticates and authorises approved researchers.
- Queues validated tasks for the Trusted Research Environment agent to pick up and execute.
- Tracks the status of submitted tasks.

## TRE Agent

### Agent.Api

- Polls the Submission Layer to retrieve new tasks
- Submits tasks to a GA4GH TES implementation
- Monitors task execution and collects results
- Sends task outputs to the Egress service for approval
- Maintains communication flow between the Submission Layer, TES, and Egress

### Agent.Web

Web FrontEnd for the Agent.Api. Allows TRE Admins to:

- Manage the TREs Projects settings.
- Manage Users allowed to submit to the Project.
- Set DMN rules to configure Ephemeral Credentials creation.

`Agent/agent-web` holds a Next.js frontend for the Agent API. CI builds its container image, but
no chart in `charts/` deploys it — `Agent.Web` (the `agent-ui` image) is the deployed UI.

## Credentials

### Credentials.Camunda

- Contains core logic handlers for TRE Agent to manage user credentials to access the TRE’s database
- Creates and revokes ephemeral user accounts to access the TRE’s database.
- Uses Camunda, Vault and LDAP services.

### Credentials.Models

- Contains Models and Services shared between Credentials.Camunda and Agent.Api that facilitate creating and revoking
  ephemeral user credentials.

## Shared

### Five Safes TES Core Library

- A shared library that includes Models, Services and Settings shared across the TRE Agent, Submission and Credentials.

## Helm charts

`charts/` holds six charts, one family (`agent`/`submission`) times three shapes:

| Chart | Shape | What it deploys |
|---|---|---|
| [`agent`](charts/agent/README.md) | standalone | The TRE Agent apps (`Agent.Api`, `Agent.Web`, `Credentials.Camunda`) |
| [`submission`](charts/submission/README.md) | standalone | The Submission apps (`Submission.Api`, `Submission.Web`) |
| [`agent-stack`](charts/agent-stack/README.md) | stack | `agent` plus its production dependencies (Postgres, RabbitMQ, RustFS, Seq, Vault, Camunda/Zeebe) |
| [`submission-stack`](charts/submission-stack/README.md) | stack | `submission` plus its production dependencies (Postgres, RabbitMQ, RustFS, Seq, Vault) |
| [`agent-devstack`](charts/agent-devstack/README.md) | devstack | Local-only stand-ins for what `agent-stack`'s production dependencies provide (Keycloak, secrets, Adminer, a disposable TRE data database) |
| [`submission-devstack`](charts/submission-devstack/README.md) | devstack | Local-only stand-ins for what `submission-stack`'s production dependencies provide (Keycloak, secrets, Adminer) |

Each chart's own README has its install and values reference. `stack` and `devstack` charts are
never installed on a shared cluster; locally, a family's devstack installs together
with its stack — see each README's own install order.

The `agent`, `submission`, and two `*-stack` charts publish independently to
`harbor.federated-analytics.ac.uk/5s-tes/chart/<name>` via their own GitHub Actions workflows
(`.github/workflows/<name>-chart.yaml`), versioned by a PR release label
(`patch|minor|major: <name>-chart`). The `*-devstack` charts are never published — they
install only from the working tree.

## Running apps from VS Code against kind

`dev-env-setup/` (`./cluster-setup.sh`) brings up a `kind` cluster with both product families'
real dependencies (Postgres, RabbitMQ, RustFS, Seq, Vault, Zeebe, LDAP, Keycloak). Each app can
then run natively from VS Code / `dotnet run` against those dependencies, using an
`appsettings.Development_Kind.json` profile that sets the same keys as
`appsettings.Development.json` to kind's localhost NodePorts (the as-built tables in
`charts/submission-devstack/README.md` and `charts/agent-devstack/README.md`'s own "Host access
for development" sections) and ingress hosts. Values are the `*-devstack` charts' fixed dev
Secrets and realm — the documented dev/prod interface, not new secret material.

Select the profile with `ASPNETCORE_ENVIRONMENT=Development_Kind`; give each app its own
`ASPNETCORE_URLS` so several can run at once without colliding:

| App | Run from host | Talks to |
|---|---|---|
| `Submission/Submission.Api` | `ASPNETCORE_ENVIRONMENT=Development_Kind ASPNETCORE_URLS=http://localhost:7163 dotnet run --no-launch-profile` | submission Postgres/RabbitMQ/RustFS/Seq/Vault (dev-access NodePorts), Keycloak `http://keycloak.submission.localtest.me/realms/Dare-Control` |
| `Submission/Submission.Web` | `ASPNETCORE_ENVIRONMENT=Development_Kind ASPNETCORE_URLS=http://localhost:5179 dotnet run --no-launch-profile` | the Submission.Api above (`http://localhost:7163`), same Keycloak realm |
| `Agent/Agent.Api` | `ASPNETCORE_ENVIRONMENT=Development_Kind ASPNETCORE_URLS=http://localhost:5269 dotnet run --no-launch-profile` | agent Postgres/RabbitMQ/RustFS/Seq/Vault/Zeebe (dev-access NodePorts), Keycloak `http://keycloak.agent.localtest.me/realms/Dare-TRE`, in-cluster Submission API/Keycloak via ingress |
| `Agent/Agent.Web` | `ASPNETCORE_ENVIRONMENT=Development_Kind ASPNETCORE_URLS=http://localhost:5233 dotnet run --no-launch-profile` | the Agent.Api above (`http://localhost:5269`), agent Keycloak |
| `Credentials/Credentials.Camunda` | `ASPNETCORE_ENVIRONMENT=Development_Kind ASPNETCORE_URLS=http://localhost:65170 dotnet run --no-launch-profile` | agent Zeebe/LDAP/Vault/Postgres (dev-access NodePorts) |

`--no-launch-profile` is required: `Properties/launchSettings.json`'s own `environmentVariables`
(`ASPNETCORE_ENVIRONMENT=Development`) otherwise wins over a shell-exported value. `dotnet`'s
environment-specific `appsettings.{ENVIRONMENT}.json` loading needs no other code change —
confirmed live: `Hosting environment: Development_Kind` in the boot log, `appsettings.Development_Kind.json`
picked up.

Keycloak: both the browser and the app's own server-side calls use the ingress host
(`keycloak.<family>.localtest.me`) — it resolves from the host (`*.localtest.me` → `127.0.0.1` →
kind's mapped port 80) and from in-cluster pods (CoreDNS rewrite in `cluster-setup.sh`), so one
value works both ways. Submission's `Authority` carries a trailing slash (its OIDC handler derives
metadata by relative resolution against it); Agent's `Authority`/`MetadataAddress` are the full
`.well-known` URL — both shapes copied from the charts' own templates
(`charts/submission/templates/api/deployment.yaml`, `charts/agent/templates/api/deployment.yaml`).

### Avoiding double consumers: turn off the in-cluster copy first

Running an app from the host while its in-cluster copy is also running means two processes
sharing one RabbitMQ queue, one Hangfire schema, or one Zeebe job type — `helm upgrade` the
family's product release (`submission` / `agent`, the direct helm installs named in
`dev-env-setup/cluster-setup.sh`) with the component turned off, re-supplying its own
`-f` values file so nothing else in it is reset:

```bash
cd dev-env-setup

# Before running Submission.Api and/or Submission.Web from the host:
helm upgrade submission ../charts/submission --namespace 5s-tes-submission \
  -f files/values/submission-product-local.yaml \
  --set api.enabled=false --set ui.enabled=false --kube-context kind-5s-tes

# Before running Agent.Api from the host:
helm upgrade agent ../charts/agent --namespace 5s-tes-agent \
  -f files/values/agent-product-local.yaml \
  --set api.enabled=false --kube-context kind-5s-tes

# Restore afterwards (drop the --set flags, keep the same -f file):
helm upgrade submission ../charts/submission --namespace 5s-tes-submission \
  -f files/values/submission-product-local.yaml --kube-context kind-5s-tes
helm upgrade agent ../charts/agent --namespace 5s-tes-agent \
  -f files/values/agent-product-local.yaml --kube-context kind-5s-tes
```

`Credentials.Camunda` needs no component-off step for its own dev-access dependencies
(Keycloak/Zeebe/LDAP/Vault/Postgres are shared read/connect targets, not single-consumer
queues); its LDAP path additionally needs
`openldap.enabled=true` set on **both** `agent-devstack` and `agent-stack` (own `-f` file +
`--set openldap.enabled=true` on each, same pattern as above) — see `charts/agent-devstack/README.md`
"Optional: local OpenLDAP". Revert with the same `--set openldap.enabled=false` (or drop the flag)
afterwards.

### Verified live (2026-08-29, kind cluster `5s-tes`)

- **Submission.Api**: booted against kind Postgres (`localhost:30432`), EF Core confirmed
  migrations already applied, `/health` → 200.
- **Submission.Web**: a real `[Authorize]` action returned Keycloak's genuine login page; a
  scripted `dev`/`password123` form-post completed the full OIDC code exchange back to
  `/signin-oidc` and rendered the authenticated Projects page — a complete login, not just the
  redirect chain.
- **Agent.Api**: `/health` → 200; boot log (`Zeebe.Client.ZeebeClient`, debug level) showed
  `Connect to http://localhost:30500`.
- **Credentials.Camunda**: boot log showed `Connected to Zeebe cluster`, 9 job workers created,
  and all 4 BPMN process models deployed to the kind Zeebe. LDAP now has a real seeded tree
  (`dc=camundaephemeral,dc=local`, `ou=Users`, `ou=groups`, `cn=trinogroup` — see
  `charts/agent-stack/templates/openldap.yaml`'s `customLdifFiles`); `ConnectionStrings:TREPostgresConnection`
  reaches the `tredata` stand-in database on its own dev-access NodePort (`localhost:31433`,
  `dev-tredata`). Vault read/write with `dev-only-token` verified directly.
- In-cluster `submission`/`agent` were restored afterward; all ArgoCD Applications in both
  namespaces reported `Synced`/`Healthy` and all Deployments `Available`.

[5s-tes-logo]: https://raw.githubusercontent.com/federated-research/docs/refs/heads/main/website/public/logos/five-safes-tes/five_safes_tes_primary.svg
[5s-tes-docs]: https://docs.federated-analytics.ac.uk/five_safes_tes
[docs-badge]: https://img.shields.io/badge/docs-black?style=for-the-badge&labelColor=%23222

[release-badge]: https://img.shields.io/github/v/release/SwanseaUniversityMedical/5s-Tes?style=for-the-badge&labelColor=%23222
[release]: https://github.com/SwanseaUniversityMedical/5s-Tes/releases

[net-badge]: https://img.shields.io/badge/.NET-512BD4?style=for-the-badge&logo=.net&logoColor=white
