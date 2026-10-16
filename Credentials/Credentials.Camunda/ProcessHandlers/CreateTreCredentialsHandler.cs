using System.Diagnostics;
using Credentials.Camunda.Services;
using Credentials.Models.DbContexts;
using Credentials.Models.Models.Zeebe;
using Zeebe.Client.Accelerator.Abstractions;
using Zeebe.Client.Accelerator.Attributes;

namespace Credentials.Camunda.ProcessHandlers
{
    /// <summary>
    /// Handler for tre/custom credentials that don't fit standard types (Postgres, Trino).
    /// Allows TRES to define their own environment variables and store them in Vault.
    /// Vault Path: tre/{user}/{submissionId}/{project}
    /// Stored Data: All provided env variables
    /// </summary>
    [JobType("create-tre-credentials")]
    public class CreateTreCredentialsHandler : CreateCredentialHandlerBase
    {
        private readonly ILogger<CreateTreCredentialsHandler> _logger;

        public CreateTreCredentialsHandler(
            ILogger<CreateTreCredentialsHandler> logger,
            IVaultCredentialsService vaultCredentialsService,
            CredentialsDbContext credentialsDbContext)
            : base(vaultCredentialsService, credentialsDbContext, logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Splits the rows into the groups that become vault paths, keyed by image code with
        /// the empty string for the variables that apply to every image.
        /// A row may name several image codes, in which case it belongs to each of their
        /// groups - it is a fan-out, not a partition, so the same variable can be shared by
        /// a handful of images without duplicating the row.
        /// </summary>
        private static SortedDictionary<string, List<CredentialsCamundaOutput>> GroupByImageCode(
            List<CredentialsCamundaOutput> envList)
        {
            var groups = new SortedDictionary<string, List<CredentialsCamundaOutput>>(StringComparer.Ordinal);

            foreach (var credential in envList)
            {
                var codes = credential.imageCode.Count == 0
                    ? new List<string> { string.Empty }   // applies to every image
                    : credential.imageCode;

                foreach (var code in codes)
                {
                    if (!groups.TryGetValue(code, out var group))
                    {
                        group = new List<CredentialsCamundaOutput>();
                        groups[code] = group;
                    }

                    group.Add(credential);
                }
            }

            return groups;
        }

        /// <summary>
        /// Builds the values to store, reading the real value out of Vault for any row the
        /// DMN flagged as a secret.
        ///
        /// This is the only credential handler that needs it. The others mint their own
        /// values - a Postgres or Trino user's password is generated here, so there is no
        /// literal value for isSecret to override. This handler is a passthrough for
        /// whatever the DMN supplies, so it is the one place a vault reference makes sense.
        ///
        /// Any secret that cannot be read is collected into <paramref name="unreadable"/>
        /// rather than stored blank or quietly skipped, and the caller fails the job on it.
        /// Secret values are never logged.
        /// </summary>
        private async Task<Dictionary<string, object>> BuildCredentialDataResolvingSecretsAsync(
            List<CredentialsCamundaOutput> envList,
            List<string> unreadable)
        {
            var credentialData = new Dictionary<string, object>();

            foreach (var credential in envList)
            {
                if (!IsSecret(credential))
                {
                    credentialData[credential.env] = credential.value;
                    continue;
                }

                var secret = await ReadSecretAsync(credential);

                if (string.IsNullOrEmpty(secret))
                {
                    _logger.LogError(
                        "Could not read the secret for environment variable {Env} from vault path {VaultPath}",
                        credential.env, credential.vaultPath);

                    if (!unreadable.Contains(credential.env, StringComparer.Ordinal))
                    {
                        unreadable.Add(credential.env);
                    }

                    continue;
                }

                credentialData[credential.env] = secret;
            }

            return credentialData;
        }

        /// <summary>
        /// A row is a secret when the DMN gave it a vault path to read the value from.
        /// </summary>
        private static bool IsSecret(CredentialsCamundaOutput credential)
        {
            return string.Equals(credential.isSecret, "Y", StringComparison.OrdinalIgnoreCase)
                   && !string.IsNullOrWhiteSpace(credential.vaultPath);
        }

        /// <summary>
        /// Reads one secret from the path the DMN row points at. The vault client builds
        /// v1/{engine}/data/{path}, so a leading slash would produce a double slash and a 404.
        /// </summary>
        private async Task<string?> ReadSecretAsync(CredentialsCamundaOutput credential)
        {
            var path = credential.vaultPath!.Trim().Trim('/');
            var secret = await _vaultCredentialsService.GetCredentialAsync(path);

            if (secret is null || secret.Count == 0)
            {
                return null;
            }

            // The environment variable name is the key by convention.
            if (secret.TryGetValue(credential.env, out var byName))
            {
                return byName?.ToString();
            }

            // A secret provisioned by hand may hold a single unnamed value instead.
            if (secret.Count == 1)
            {
                _logger.LogInformation(
                    "Secret at {VaultPath} has no '{Env}' field, using its single field '{Field}'",
                    path, credential.env, secret.Keys.First());

                return secret.Values.First()?.ToString();
            }

            _logger.LogError(
                "Secret at {VaultPath} has no '{Env}' field and holds {Count} fields, so the value is ambiguous. Fields: {Fields}",
                path, credential.env, secret.Count, string.Join(", ", secret.Keys));

            return null;
        }

        /// <summary>
        /// Handle tre/custom credentials creation.
        /// Stores user-provided environment variables in Vault without modification.
        /// </summary>
        public override async Task<Dictionary<string, object>> HandleJob(
            ZeebeJob job,
            CancellationToken cancellationToken)
        {
            var sw = Stopwatch.StartNew();
            _logger.LogDebug("CreatetreCredentialsHandler started. processInstance={ProcessInstanceKey}",
                job.ProcessInstanceKey);

            string? submissionId = null;
            long? parentProcessKey = null;
            long processInstanceKey = job.ProcessInstanceKey;

            try
            {
                var extraction = ExtractCredentials(job);
                submissionId = extraction.SubmissionId;
                parentProcessKey = extraction.ParentProcessKey;

                // Refuse to provision unless this submission was approved by Agent.Api
                if (!await IsSubmissionApprovedAsync(extraction))
                {
                    await RecordErrorAsync(submissionId, parentProcessKey, processInstanceKey, "postgres", "No matching approved submission record found");
                    return CreateStatusResponse("ERROR: Submission not approved.");
                }

                if (extraction.EnvList?.FirstOrDefault() == null)
                {
                    await RecordErrorAsync(submissionId, parentProcessKey, processInstanceKey, "tre",
                        "No credential information found in envList");
                    return CreateStatusResponse("ERROR: Missing credentials, cannot proceed.");
                }

                if (string.IsNullOrEmpty(extraction.User) || string.IsNullOrEmpty(extraction.Project))
                {
                    await RecordErrorAsync(submissionId, parentProcessKey, processInstanceKey, "tre",
                        "Missing user or project; cannot proceed with tre credentials.");
                    return CreateStatusResponse("ERROR: Missing user or project information.");
                }

                _logger.LogInformation(
                    "Processing tre credentials for user: {User}, project: {Project}",
                    extraction.User, extraction.Project);

                string basePath = $"tre/{extraction.User}/{submissionId}/{extraction.Project}";

                // Variables that name no image code go to the base path and reach every
                // executor. Ones that name an image code go a level deeper, so the Agent can
                // pick up only the group belonging to the image it is about to run.
                var groups = GroupByImageCode(extraction.EnvList);

                // Resolve every group before storing any of them. A secret that cannot be
                // read fails the whole job, and failing before the first write keeps Vault
                // free of a half-populated set of paths.
                var resolved = new List<(string VaultPath, Dictionary<string, object> Data)>();
                var unreadable = new List<string>();

                foreach (var group in groups)
                {
                    var credentialData = await BuildCredentialDataResolvingSecretsAsync(group.Value, unreadable);

                    if (credentialData.Count == 0)
                    {
                        continue;
                    }

                    resolved.Add((group.Key.Length == 0 ? basePath : $"{basePath}/{group.Key}", credentialData));
                }

                if (unreadable.Count > 0)
                {
                    // Nothing downstream checks that an expected variable arrived, so a
                    // missing secret would otherwise reach the container as a silent gap.
                    var message = $"Could not read {unreadable.Count} secret(s) from vault: {string.Join(", ", unreadable)}";

                    _logger.LogError("{Message} for submission {SubmissionId}", message, submissionId);
                    await RecordErrorAsync(submissionId, parentProcessKey, processInstanceKey, "tre", message);

                    return CreateStatusResponse("ERROR: " + message);
                }

                var storedPaths = new List<string>();

                foreach (var (vaultPath, credentialData) in resolved)
                {
                    _logger.LogInformation("Storing {Count} tre variable(s) at vault path: {VaultPath}",
                        credentialData.Count, vaultPath);

                    if (!await StoreInVaultAsync(submissionId, parentProcessKey, processInstanceKey,
                        vaultPath, credentialData, "tre"))
                    {
                        return CreateStatusResponse("ERROR: Credential storage in vault failed");
                    }

                    await CreateCredentialsReadyMessageAsync(submissionId, parentProcessKey,
                        processInstanceKey, vaultPath, "tre");

                    storedPaths.Add(vaultPath);
                }

                _logger.LogInformation(
                    "Successfully stored tre credentials for project: {Project} at {Count} path(s): {VaultPaths}",
                    extraction.Project, storedPaths.Count, string.Join(", ", storedPaths));

                return CreateStatusResponse(
                    $"OK: tre credentials stored for project '{extraction.Project}' at {storedPaths.Count} path(s).");
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("CreateTreCredentialsHandler was cancelled");
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Unexpected error in CreateTreCredentialsHandler. processInstance={ProcessInstanceKey}",
                    processInstanceKey);

                await RecordErrorAsync(submissionId, parentProcessKey, processInstanceKey, "tre",
                    $"Unexpected error: {ex.Message}");

                return CreateStatusResponse("ERROR: Unexpected error in tre handler");
            }
            finally
            {
                if (sw.IsRunning) sw.Stop();
                _logger.LogInformation("CreateTreCredentialsHandler took {Seconds} seconds",
                    sw.Elapsed.TotalSeconds);
            }
        }
    }
}
