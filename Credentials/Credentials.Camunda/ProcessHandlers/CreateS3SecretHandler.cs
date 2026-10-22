using System.Diagnostics;
using Credentials.Camunda.Services;
using Credentials.Models.DbContexts;
using IMinioHelper = FiveSafesTes.Core.Services.IMinioHelper;
using Zeebe.Client.Accelerator.Abstractions;
using Zeebe.Client.Accelerator.Attributes;

namespace Credentials.Camunda.ProcessHandlers
{
    /// <summary>
    /// Creates an ephemeral, per-submission S3 (RustFS/MinIO) access key: a scoped canned
    /// policy, an access-key/secret pair, the policy attached to that key, and the secret
    /// stored in Vault. Mirrors the Postgres/Trino create handlers.
    /// </summary>
    [JobType("create-s3-secret")]
    public class CreateS3SecretHandler : CreateCredentialHandlerBase
    {
        private readonly IMinioHelper _minioHelper;

        public CreateS3SecretHandler(
            ILogger<CreateS3SecretHandler> logger,
            IMinioHelper minioHelper,
            IVaultCredentialsService vaultCredentialsService,
            CredentialsDbContext credentialsDbContext)
            : base(vaultCredentialsService, credentialsDbContext, logger)
        {
            _minioHelper = minioHelper;
        }

        public override async Task<Dictionary<string, object>> HandleJob(ZeebeJob job, CancellationToken cancellationToken)
        {
            var sw = Stopwatch.StartNew();
            _logger.LogDebug("CreateS3SecretHandler started. processInstance={ProcessInstanceKey}", job.ProcessInstanceKey);

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
                    await RecordErrorAsync(submissionId, parentProcessKey, processInstanceKey, "s3",
                        "No credential information found in envList");
                    return CreateStatusResponse("ERROR: Missing credentials, cannot proceed.");
                }

                // S3-specific variables from the DMN envList.
                string? accessKey = extraction.EnvList
                    .Where(x => x.env.Equals("accessKey", StringComparison.OrdinalIgnoreCase))
                    .FirstOrDefault()?.value?.ToString();

                string? endPoint = extraction.EnvList
                    .Where(x => x.env.Equals("endPoint", StringComparison.OrdinalIgnoreCase))
                    .FirstOrDefault()?.value?.ToString();

                string? submissionBucket = extraction.EnvList
                    .Where(x => x.env.Equals("submissionBucket", StringComparison.OrdinalIgnoreCase))
                    .FirstOrDefault()?.value?.ToString();

                string? outputBucket = extraction.EnvList
                    .Where(x => x.env.Equals("outputBucket", StringComparison.OrdinalIgnoreCase))
                    .FirstOrDefault()?.value?.ToString();

                // Fall back to a single "bucket" entry (or the project name) when discrete
                // submission/output buckets are not supplied by the DMN.
                var fallbackBucket = extraction.EnvList
                    .Where(x => x.env.Equals("bucket", StringComparison.OrdinalIgnoreCase))
                    .FirstOrDefault()?.value?.ToString() ?? extraction.Project;

                if (string.IsNullOrEmpty(submissionBucket)) submissionBucket = fallbackBucket;
                if (string.IsNullOrEmpty(outputBucket)) outputBucket = fallbackBucket;

                if (string.IsNullOrEmpty(accessKey) || string.IsNullOrEmpty(submissionId) ||
                    string.IsNullOrEmpty(extraction.User) || string.IsNullOrEmpty(extraction.Project) ||
                    string.IsNullOrEmpty(submissionBucket) || string.IsNullOrEmpty(outputBucket))
                {
                    await RecordErrorAsync(submissionId, parentProcessKey, processInstanceKey, "s3",
                        "Missing credentials; cannot proceed with S3 secret creation.");
                    return CreateStatusResponse("ERROR: Missing credentials, cannot proceed.");
                }

                var secretKey = GenerateSecurePassword();
                var policyName = $"{accessKey}-policy";

                // Step 1: canned policy scoped to the project's buckets (read submission,
                // read/write/delete output). Same shape as the persistent ProjectS3AccessKeyService,
                // so ephemeral and persistent credentials share one proven policy model.
                var policyCreated = await _minioHelper.CreateProjectS3AccessPolicyAsync(
                    policyName, submissionBucket, outputBucket);
                if (!policyCreated)
                {
                    await RecordErrorAsync(submissionId, parentProcessKey, processInstanceKey, "s3",
                        $"Failed to create S3 access policy {policyName}");
                    return CreateStatusResponse("ERROR: Failed credential creation");
                }

                // Step 2: access key + secret pair.
                var userCreated = await _minioHelper.CreateMinioSecretAsync(accessKey, secretKey, cancellationToken);
                if (!userCreated.Success)
                {
                    await RecordErrorAsync(submissionId, parentProcessKey, processInstanceKey, "s3",
                        $"Failed to create S3 access key {accessKey}: {userCreated.Error}");
                    return CreateStatusResponse("ERROR: Failed credential creation");
                }

                // Step 3: attach the scoped policy to the access key.
                var policyAttached = await _minioHelper.AttachPolicyToUserAsync(policyName, accessKey, cancellationToken);
                if (!policyAttached)
                {
                    await RecordErrorAsync(submissionId, parentProcessKey, processInstanceKey, "s3",
                        $"Failed to attach policy {policyName} to access key {accessKey}");
                    return CreateStatusResponse("ERROR: Failed credential creation");
                }

                // Store secret in Vault. BuildCredentialData only substitutes password-named
                // fields, so set the generated secret and endpoint explicitly.
                var credentialData = BuildCredentialData(extraction.EnvList, secretKey);
                credentialData["secretKey"] = secretKey;
                if (!credentialData.ContainsKey("endPoint") ||
                    string.IsNullOrEmpty(credentialData["endPoint"]?.ToString()))
                {
                    credentialData["endPoint"] = endPoint ?? string.Empty;
                }

                string vaultPath = $"s3/{extraction.User}/{submissionId}/{extraction.Project}";
                if (!await StoreInVaultAsync(submissionId, parentProcessKey, processInstanceKey, vaultPath, credentialData, "s3"))
                    return CreateStatusResponse("ERROR: Credential store in vault failed");

                await CreateCredentialsReadyMessageAsync(submissionId, parentProcessKey, processInstanceKey, vaultPath, "s3");

                _logger.LogInformation("Successfully created S3 access key: {AccessKey} for project: {Project}",
                    accessKey, extraction.Project);
                return CreateStatusResponse($"OK: S3 access key '{accessKey}' created for project '{extraction.Project}'.");
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in CreateS3SecretHandler. processInstance={ProcessInstanceKey}",
                    processInstanceKey);
                await RecordErrorAsync(submissionId, parentProcessKey, processInstanceKey, "s3",
                    $"Unexpected error: {ex.Message}");
                return CreateStatusResponse("Unexpected Error in S3 handler");
            }
            finally
            {
                if (sw.IsRunning) sw.Stop();
                _logger.LogInformation("CreateS3SecretHandler took {Seconds} seconds", sw.Elapsed.TotalSeconds);
            }
        }
    }
}
