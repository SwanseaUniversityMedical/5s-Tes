using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;
using Credentials.Camunda.Models;
using Credentials.Camunda.Services;
using Credentials.Models.DbContexts;
using Credentials.Models.Models;
using Credentials.Models.Models.Zeebe;
using Zeebe.Client.Accelerator.Abstractions;

namespace Credentials.Camunda.ProcessHandlers
{
    /// <summary>
    /// Base class for credential handlers that eliminates code duplication.
    /// Provides common functionality for creating users in various credential systems (Postgres, Trino, etc.)
    /// </summary>
    public abstract class CreateCredentialHandlerBase : IAsyncZeebeWorkerWithResult<Dictionary<string, object>>
    {
        protected readonly IVaultCredentialsService _vaultCredentialsService;
        protected readonly CredentialsDbContext _credentialsDbContext;
        protected readonly ILogger _logger;

        protected CreateCredentialHandlerBase(
            IVaultCredentialsService vaultCredentialsService,
            CredentialsDbContext credentialsDbContext,
            ILogger logger)
        {
            _vaultCredentialsService = vaultCredentialsService;
            _credentialsDbContext = credentialsDbContext;
            _logger = logger;
        }

        /// <summary>
        /// Extract common variables from Zeebe job JSON
        /// </summary>
        protected static CredentialExtraction ExtractCredentials(ZeebeJob job)
        {
            var variables = JsonSerializer.Deserialize<Dictionary<string, object>>(job.Variables);
            var envListJson = variables["envList"]?.ToString();
            var envList = JsonSerializer.Deserialize<List<CredentialsCamundaOutput>>(envListJson);

            string? project = variables["project"]?.ToString()
                .Replace("[", "").Replace("]", "").Replace("\"", "");
            string? user = variables["user"]?.ToString()
                .Replace("[", "").Replace("]", "");

            var subItem = envList?.FirstOrDefault(x =>
                string.Equals(x.env, "submissionId", StringComparison.OrdinalIgnoreCase));
            string? submissionId = subItem?.value;

            long? parentProcessKey = null;
            if (variables.TryGetValue("parentProcessKey", out var parentObj))
            {
                if (parentObj is JsonElement el && el.ValueKind == JsonValueKind.Number && el.TryGetInt64(out var parsed))
                    parentProcessKey = parsed;
                else if (long.TryParse(parentObj?.ToString(), out var parsed2))
                    parentProcessKey = parsed2;
            }

            return new CredentialExtraction
            {
                Variables = variables,
                EnvList = envList,
                SubmissionId = submissionId,
                ParentProcessKey = parentProcessKey,
                Project = project,
                User = user
            };
        }

        /// <summary>
        /// Record error to database for tracking failed credential creation
        /// </summary>
        protected async Task RecordErrorAsync(string? submissionId, long? parentProcessKey, long processInstanceKey,
            string credentialType, string errorMessage)
        {
            try
            {
                if (string.IsNullOrEmpty(submissionId) || !int.TryParse(submissionId, out var submission))
                    return;

                var existing = await _credentialsDbContext.EphemeralCredentials
                    .FirstOrDefaultAsync(x => x.SubmissionId == submission && x.ProcessInstanceKey == processInstanceKey);

                if (existing != null)
                {
                    _logger.LogWarning(
                        "EphemeralCredential already exists for SubmissionId={SubmissionId} and ProcessKey={ProcessInstanceKey}",
                        submissionId, processInstanceKey);
                    return;
                }

                var row = new EphemeralCredential
                {
                    SubmissionId = submission,
                    ParentProcessInstanceKey = parentProcessKey,
                    ProcessInstanceKey = processInstanceKey,
                    CreatedAt = DateTime.UtcNow,
                    IsProcessed = false,
                    CredentialType = credentialType,
                    VaultPath = null,
                    SuccessStatus = SuccessStatus.Error,
                    ErrorMessage = errorMessage
                };

                _credentialsDbContext.EphemeralCredentials.Add(row);
                await _credentialsDbContext.SaveChangesAsync();

                _logger.LogInformation("Recorded error for processInstance={ProcessInstanceKey}: {Message}",
                    processInstanceKey, errorMessage);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to record error. processInstance={ProcessInstanceKey}", processInstanceKey);
            }
        }

        /// <summary>
        /// Record successful credential creation to database
        /// </summary>
        protected async Task CreateCredentialsReadyMessageAsync(string? submissionId, long? parentProcessKey,
            long processInstanceKey, string vaultPath, string credentialType)
        {
            try
            {
                if (string.IsNullOrEmpty(submissionId) || !int.TryParse(submissionId, out var submissionGuid))
                    return;

                var existing = await _credentialsDbContext.EphemeralCredentials
                    .FirstOrDefaultAsync(x => x.SubmissionId == submissionGuid && x.ProcessInstanceKey == processInstanceKey);

                if (existing != null)
                {
                    _logger.LogWarning(
                        "EphemeralCredential already exists for SubmissionId={SubmissionId}, ProcessInstanceKey={ProcessInstanceKey}",
                        submissionId, processInstanceKey);
                    return;
                }

                var credReadyMessage = new EphemeralCredential
                {
                    SubmissionId = submissionGuid,
                    ParentProcessInstanceKey = parentProcessKey,
                    ProcessInstanceKey = processInstanceKey,
                    CreatedAt = DateTime.UtcNow,
                    IsProcessed = false,
                    VaultPath = vaultPath,
                    CredentialType = credentialType,
                    SuccessStatus = SuccessStatus.Success
                };

                _credentialsDbContext.EphemeralCredentials.Add(credReadyMessage);
                await _credentialsDbContext.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating credentials ready message for submission: {SubmissionId}", submissionId);
            }
        }

        /// <summary>
        /// Create standardized status response dictionary
        /// </summary>
        protected static Dictionary<string, object> CreateStatusResponse(string text)
        {
            return new Dictionary<string, object> { ["statusText"] = text };
        }

        /// <summary>
        /// Generate a secure random password
        /// </summary>
        protected static string GenerateSecurePassword()
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789!@#$%^&*";
            var random = new Random();
            return new string(Enumerable.Repeat(chars, 16)
                .Select(s => s[random.Next(s.Length)]).ToArray());
        }

        /// <summary>
        /// Store credential data in Vault, with error handling
        /// </summary>
        protected async Task<bool> StoreInVaultAsync(string? submissionId, long? parentProcessKey,
            long processInstanceKey, string vaultPath, Dictionary<string, object> credentialData, string credentialType)
        {
            var vaultResult = await _vaultCredentialsService.AddCredentialAsync(vaultPath, credentialData);
            if (!vaultResult)
            {
                await RecordErrorAsync(submissionId, parentProcessKey, processInstanceKey, credentialType,
                    $"Failed to store credential in Vault at path: {vaultPath}");
                return false;
            }
            return true;
        }

        /// <summary>
        /// Build credential data dictionary from environment list, replacing password if generated
        /// </summary>
        protected static Dictionary<string, object> BuildCredentialData(
            List<CredentialsCamundaOutput> envList, string? generatedPassword)
        {
            var credentialData = new Dictionary<string, object>();
            foreach (var credential in envList)
            {
                var credentialEnv = new CredentialsVault
                {
                    env = credential.env,
                    value = credential.env.ToLower().Contains("password") ? generatedPassword : credential.value
                };
                credentialData.Add(credentialEnv.env, credentialEnv.value);
            }
            return credentialData;
        }

        /// <summary>
        /// Abstract method to be implemented by derived classes
        /// </summary>
        public abstract Task<Dictionary<string, object>> HandleJob(ZeebeJob job, CancellationToken cancellationToken);
    }

    /// <summary>
    /// Data transfer object for extracted credentials from Zeebe job
    /// </summary>
    public class CredentialExtraction
    {
        public Dictionary<string, object> Variables { get; set; }
        public List<CredentialsCamundaOutput> EnvList { get; set; }
        public string? SubmissionId { get; set; }
        public long? ParentProcessKey { get; set; }
        public string? Project { get; set; }
        public string? User { get; set; }
    }
}
