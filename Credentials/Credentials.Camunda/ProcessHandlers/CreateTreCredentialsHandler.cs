using System.Diagnostics;
using Credentials.Camunda.Services;
using Credentials.Models.DbContexts;
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

                var credentialData = BuildCredentialData(extraction.EnvList, null);

                string vaultPath = $"tre/{extraction.User}/{submissionId}/{extraction.Project}";

                _logger.LogInformation("Storing tre credentials at vault path: {VaultPath}", vaultPath);

                if (!await StoreInVaultAsync(submissionId, parentProcessKey, processInstanceKey,
                    vaultPath, credentialData, "tre"))
                {
                    return CreateStatusResponse("ERROR: Credential storage in vault failed");
                }

                await CreateCredentialsReadyMessageAsync(submissionId, parentProcessKey,
                    processInstanceKey, vaultPath, "tre");

                _logger.LogInformation(
                    "Successfully stored tre credentials for project: {Project} at path: {VaultPath}",
                    extraction.Project, vaultPath);

                return CreateStatusResponse(
                    $"OK: tre credentials stored for project '{extraction.Project}'.");
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
