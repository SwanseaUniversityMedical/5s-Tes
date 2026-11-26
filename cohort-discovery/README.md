# Cohort Discovery (local Docker setup)

A local, standalone Docker Compose stack for the Cohort Discovery tool, wired up to allow users to log in via the same Keycloak instance used by the 5s-Tes Submission Layer (`Dare-Control` realm). 

## Running it

```
docker compose up -d
```

This starts five containers:

| Service | Purpose | Local URL |
| --- | --- | --- |
| `mysql` | App + OMOP databases | `localhost:3306` |
| `redis` | Queue/cache backend | internal only |
| `nlp` | NLP resolver service | `localhost:5001` |
| `api` | Laravel API | `localhost:8102` |
| `web` | Next.js frontend | `localhost:3001` |

Login at [http://localhost:3001](http://localhost:3001) via the "Single Sign-On" option, using a user from the `Dare-Control` Keycloak realm.

### Images

`api`, `nlp` and `web` are pulled from Harbor (`harbor.ukserp.ac.uk/cohort-discovery/...`) rather than built locally, so you'll need to `docker login harbor.ukserp.ac.uk` first (requires access to the `cohort-discovery` project there; ask a Harbor admin if it's not visible under your account).

### Keycloak

`SSO_DEFAULT_*` in `.env` point at the `5S-TES-deployment/DemoStack` local Keycloak (`http://keycloak.localtest.me:8085/realms/Dare-Control`). That Keycloak needs to already be running, and its `Cohort-Discovery` client (in `sub-layer.json`) needs the matching redirect URI and secret.

## How user access is kept in sync

### Linking users to workgroups

Despite using Keycloak, the cohort discovery tool still needs to create its own user accounts that the keycloak logins resolve to. These accounts do not come into existence until you log into the cohort discovery tool for the first time, so in `CohortDiscoverySyncService.SyncProject`, we cannot add the project users to a work group as we cannot reliably tell whether they have logged into the cohort tool before. There is no endpoint in the cohort discovery tool to create users. If there was, we would be able to create a user and add them to a workgroup immediately as the project syncs.

For demo purposes, an alternative solution has been devised. `CohortDiscoveryWorkgroupLinkService` is a background service used to poll the cohort discovery tool's list of users. Whenever it runs, it checks the email address of each project's TRE admin against that list. If it finds a match, they are added to their project's workgroup. If no match is found, nothing happens, and the same check is repeated the next time the service runs.

### Custodian access via Keycloak claims

Whenever a TRE's details are synced, `GovernanceImportService` sets a `custodian` attribute on that TRE's admin user directly in Keycloak, via `KeycloakAdminService.SetTreAdminCustodianAttributeAsync`. This attribute holds the name of the TRE(s) that admin is responsible for. A protocol mapper on the Cohort Discovery Keycloak client exposes this attribute as a claim on the login token, called `tre_custodian`.

When that admin logs into the cohort discovery tool via Keycloak, the tool reads this claim from the token and matches its value against its own list of custodians, granting the user access to any matching one. This happens automatically on every login, so if the attribute changes later, their access is kept in sync without anyone needing to do anything on the cohort discovery side.

The attribute itself is additive rather than replaced each time, so an admin responsible for multiple TREs ends up with access to all of them, not just whichever one was synced most recently. It does not, however, remove access automatically. If an admin is reassigned away from a TRE, they would keep access to it until someone manually removes the attribute.

### Custodian vs. workgroup

Cohort discovery grants a user access to a dataset through either of two mechanisms: custodian membership or workgroup membership. In 5s-Tes terms, holding a custodian is the equivalent of being granted access to an entire TRE, while being on a workgroup is the equivalent of being granted access to a specific Project via any TREs you may be assigned to.

### Publishing a message to RabbitMQ

Exchange: `dg-export-exchange` (direct) · Queue: `dg-export-queue` · Routing key: `dg-export-routing`

Example payload:

```json
{
  "ConsortiumId": 1,
  "Name": "Sample Consortium",
  "Description": "A sample consortium for local testing",
  "ConsortiumTres": [
    {
      "Id": 1,
      "Name": "Sample TRE",
      "Code": "SAMPLE-TRE",
      "OtherInformation": ""
    }
  ],
  "EnabledProducts": [
    {
      "Id": 1,
      "ProductMetadata": {
        "Name": "Cohort Discovery",
        "Version": "1.0"
      },
      "AssignedTres": [1]
    }
  ],
  "PresentUsers": [
    {
      "Id": 1,
      "FirstName": "Global",
      "LastName": "AdminUser",
      "Email": "globaladminuser@example.com",
      "Role": "Admin",
      "RepresentedTreId": 1
    }
  ]
}
```
