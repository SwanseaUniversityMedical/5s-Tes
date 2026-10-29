# Federated tasks

The orchestrator (Submission.Api) queues a GA4GH TES task for one TRE. The TRE Agent (Agent.Api) polls for it, reads a CSV dataset from the TRE bucket, runs `count` or `mean`, and reports the result.

There is no DAAMS signing or approval gate yet.

## Flow

1. A `dare-control-admin` user calls `POST /api/FederatedTask/Enqueue` with a TES task. Submission validates it against the federated profile. The task is stored as `QUEUED`. The response holds the task id.
2. The Agent job `Run Federated Tasks` (`JobSettings:FederatedTaskJobName`) runs every `JobSettings:federatedTaskSchedule` minutes. It calls `GET /api/FederatedTask/GetPendingTasksForTre`. The TRE is taken from the token.
3. The Agent reads the one `.csv` under `dataset/<name>/` in the dataset bucket.
4. The Agent runs the operation in-process. It does not start a container.
5. The Agent calls `POST /api/FederatedTask/ReportResult`.
6. A `dare-control-admin` user calls `GET /api/FederatedTask/GetTask?id=<taskId>`.

`GetPendingTasksForTre` and `ReportResult` accept `dare-control-admin` and `dare-tre-admin`.

## The federated TES profile

| TES field | Rule |
|---|---|
| `executors` | Exactly 1. `image` must be `5s-tes/federated-ops:1`. |
| `executors[0].command` | `[<operation>, ...args]`. The operation is `count` or `mean`. |
| command args | `--column <name>`: at most once. Required for `mean`, optional for `count`. `--filter <column>:<op>:<value>`: repeatable. |
| filter `<op>` | `eq`, `gt`, `lt`, `gte` or `lte`. Split on the first two `:` only, so a value may contain `:`. |
| `inputs` | Exactly 1. `url` is `dataset://<name>`. |
| `outputs` | Must be null or empty. The result is returned in the task log. |
| `tags` | `tres` names exactly one existing TRE. It is trimmed and lowercased, and must not contain `\|`. `study_id` must not be empty. |
| `resources`, `volumes`, `name`, `description` | Accepted and ignored. |

The dataset name must match `^[a-z0-9][a-z0-9_-]{0,62}$`.

Example: the mean of `age` on dataset `cohort_2024`, for rows where `sex` is `F`.

```json
{
  "executors": [
    {
      "image": "5s-tes/federated-ops:1",
      "command": ["mean", "--column", "age", "--filter", "sex:eq:F"]
    }
  ],
  "inputs": [
    {
      "url": "dataset://cohort_2024",
      "path": "/data/cohort_2024",
      "type": "DIRECTORY"
    }
  ],
  "outputs": [],
  "tags": {
    "tres": "my-tre",
    "study_id": "study-001"
  }
}
```

### Task state and result

| State | When |
|---|---|
| `QUEUED` | The task is enqueued. |
| `COMPLETE` | The task succeeded. |
| `EXECUTOR_ERROR` | The task could not run against the data. All error codes except `internal_error`. |
| `SYSTEM_ERROR` | `internal_error`. |

The result is in `logs[0].metadata`.

- On success: `value` and `row_count`. `value` is an invariant decimal. It is empty when there are no rows.
- On error: `error_code` and `error_message`.

### Error codes

| Code | Meaning |
|---|---|
| `invalid_task` | The task does not follow the profile. The message says which rule failed. |
| `invalid_dataset_name` | The name does not match `^[a-z0-9][a-z0-9_-]{0,62}$`. |
| `dataset_not_found` | No `.csv` file under `dataset/<name>/`. |
| `ambiguous_dataset` | More than one `.csv` file under `dataset/<name>/`. |
| `unknown_operation` | The operation is not `count` or `mean`. |
| `invalid_filter` | The operator is not `eq`, `gt`, `lt`, `gte` or `lte`, or the filter is not `<column>:<op>:<value>`. |
| `column_not_found` | The column, or a filter column, is not in the dataset. |
| `non_numeric_value` | `mean` found a value that is not a number. |
| `internal_error` | An unexpected failure, for example storage is unreachable. The message has the details. |

## Site setup

### Bucket and policy

Agent.Api creates both at startup. Settings are in `FederatedTaskSettings` in `Agent/Agent.Api/appsettings.json`.

| Setting | Default |
|---|---|
| `DatasetBucket` | `federated-datasets` |
| `DatasetPrefix` | `dataset/` |
| `AllowedExtensions` | `["csv"]` |
| `UploadPolicyName` | `dataset-upload` |

The `dataset-upload` policy allows:

- `s3:ListAllMyBuckets`, `s3:GetBucketLocation`, `s3:GetBucketPolicyStatus` on all buckets.
- `s3:ListBucket`, `s3:ListBucketMultipartUploads` on the dataset bucket.
- `s3:GetObject` on `federated-datasets/dataset/*`.
- `s3:PutObject`, `s3:AbortMultipartUpload`, `s3:ListMultipartUploadParts` on `federated-datasets/dataset/*.csv`.

It has no delete permission.

### Give a user upload access

Link the user to the policy in one of two ways. Use the one your deployment is set up for.

- Add the user to the Keycloak group `dataset-upload`. RustFS maps the `groups` claim to the policy of the same name.
- Or set the `policy` user attribute to `dataset-upload` (the attribute name is `MinioTRESettings:AttributeName`).

### RustFS OIDC settings

Set these on RustFS. Use your own values. Never commit the client secret.

```
RUSTFS_IDENTITY_OPENID_ENABLE=on
RUSTFS_IDENTITY_OPENID_CONFIG_URL=<keycloak>/realms/<realm>
RUSTFS_IDENTITY_OPENID_CLIENT_ID=<client-id>
RUSTFS_IDENTITY_OPENID_CLIENT_SECRET=<client-secret>
RUSTFS_IDENTITY_OPENID_SCOPES=openid,profile,email
RUSTFS_IDENTITY_OPENID_GROUPS_CLAIM=groups
RUSTFS_IDENTITY_OPENID_USERNAME_CLAIM=preferred_username
RUSTFS_IDENTITY_OPENID_EMAIL_CLAIM=email
RUSTFS_IDENTITY_OPENID_REDIRECT_URI=<rustfs>/rustfs/admin/v3/oidc/callback/default
RUSTFS_IDENTITY_OPENID_DISPLAY_NAME=<login button label>
```

Two things to check in Keycloak:

- The client needs a `groups` mapper of type Group Membership, with "Full group path" off.
- The Keycloak issuer hostname must be the same one that the browser and RustFS both reach.

## Uploader guide

1. Log in to the RustFS console with your TRE Keycloak account.
2. Open the bucket `federated-datasets`.
3. Use one folder per dataset under `dataset/`. Name it with the GOV-12 dataset name, for example `dataset/cohort_2024/`.
4. Put exactly one lowercase `.csv` file in the folder.
5. To replace a dataset, upload a file with the same name. It overwrites the old one.

You cannot delete files. Ask a RustFS admin if one must be removed. If a folder holds two `.csv` files, tasks fail with `ambiguous_dataset`.

## Deferred SQL datasets

SQL dump datasets are not supported yet. The plan for later:

- A dataset folder may hold a `.sql` dump instead of a CSV. It must be data only: one `CREATE TABLE` and `INSERT INTO ... VALUES` statements.
- Agent.Api checks every statement before it runs any. Any other statement rejects the whole file with a new code, `invalid_sql_dump`.
- The dump loads into an isolated in-memory SQLite database. Uploaded SQL never runs against a real database, and never as a query.
- A plain `pg_dump` will not pass. Export with `--inserts` and remove the `SET` and `SELECT` lines.
- To turn it on: add `sql` to `FederatedTaskSettings:AllowedExtensions` (this also widens the upload policy) and add a `.sql` branch to the dataset reader.

## Known issue: RustFS console loop

The RustFS console loops for non-admin users (rustfs/console#271). The fix is merged upstream. It is not in RustFS 1.0.4 yet. Storage permissions are not affected.
