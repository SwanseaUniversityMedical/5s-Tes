using System.Diagnostics;
using System.Text.Json;
using Credentials.Camunda.Services;
using Credentials.Models.Models.Zeebe;
using Zeebe.Client.Accelerator.Abstractions;

namespace Credentials.Camunda.ProcessHandlers
{
    /// <summary>
    /// Returns Task<Dictionary<string, object>> like Create handlers for consistency
    /// </summary>
    public abstract class DeleteCredentialHandlerBase : IAsyncZeebeWorkerWithResult<Dictionary<string, object>>
    {
        protected readonly ILogger _logger;
        protected readonly IVaultCredentialsService _vaultCredentialsService;
        protected readonly IEphemeralCredentialsService _ephemeralCredentialsService;
        protected abstract string CredentialType { get; }

        protected DeleteCredentialHandlerBase(
            ILogger logger,
            IVaultCredentialsService vaultCredentialsService,
            IEphemeralCredentialsService ephemeralCredentialsService)
        {
            _logger = logger;
            _vaultCredentialsService = vaultCredentialsService;
            _ephemeralCredentialsService = ephemeralCredentialsService;
        }

        /// <summary>
        /// Extract common variables from Zeebe job JSON
        /// </summary>
        protected static DeleteCredentialExtraction ExtractCredentials(ZeebeJob job)
        {
            var variables = JsonSerializer.Deserialize<Dictionary<string, object>>(job.Variables);
            var envListJson = variables["envList"]?.ToString();
            var envList = JsonSerializer.Deserialize<List<CredentialsCamundaOutput>>(envListJson);

            string? project = variables["project"]?.ToString();
            string? user = variables["user"]?.ToString();

            var submissionIdEntry = envList?.FirstOrDefault(x =>
                x.env.ToLower().Contains("submissionid"));
            string? submissionId = submissionIdEntry?.value?.ToString();

            return new DeleteCredentialExtraction
            {
                Variables = variables,
                EnvList = envList,
                SubmissionId = submissionId,
                Project = project,
                User = user
            };
        }

        /// <summary>
        /// Handle vault operations: remove credentials and update expiration
        /// </summary>
        protected async Task HandleVaultOperationsAsync(
            string? submissionId,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(submissionId) || !int.TryParse(submissionId, out int submissionIdInt))
            {
                _logger.LogWarning("Invalid or missing submissionId: {SubmissionId}", submissionId);
                return;
            }

            var vaultPath = await _ephemeralCredentialsService.GetVaultPathBySubmissionIdAsync(
                submissionIdInt, CredentialType, cancellationToken);

            if (string.IsNullOrEmpty(vaultPath))
            {
                _logger.LogWarning("No vaultPath found for submissionId: {SubmissionId}", submissionId);
                return;
            }

            _logger.LogInformation("Removing credentials from Vault at path: {VaultPath}", vaultPath);

            var vaultDeleteResult = await _vaultCredentialsService.RemoveCredentialAsync(vaultPath);

            if (vaultDeleteResult)
            {
                _logger.LogInformation("Successfully removed credentials from Vault at path: {VaultPath}", vaultPath);

                await _ephemeralCredentialsService.UpdateCredentialExpirationAsync(vaultPath, cancellationToken);
            }
            else
            {
                _logger.LogWarning("Failed to remove credentials from Vault at path: {VaultPath}", vaultPath);
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
        /// Abstract method to be implemented by derived classes for specific user deletion logic
        /// </summary>
        protected abstract Task<bool> DeleteUserAsync(string? username, CancellationToken cancellationToken);

        /// <summary>
        /// Abstract method to be implemented by derived classes for specific user existence check logic
        /// </summary>
        protected abstract Task<bool> CheckUserExistAsync(string username);        
        
        
        /// <summary>
        /// Main handler method called by Zeebe - implements common deletion workflow
        /// Returns Dictionary<string, object> for consistency with Create handlers
        /// </summary>
        public async Task<Dictionary<string, object>> HandleJob(ZeebeJob job, CancellationToken cancellationToken)
        {
            var sw = Stopwatch.StartNew();
            _logger.LogDebug("{HandlerName} started. processInstance={ProcessInstanceKey}",
                GetType().Name, job.ProcessInstanceKey);

            try
            {
                var extraction = ExtractCredentials(job);

                if (extraction.EnvList?.FirstOrDefault() == null)
                {
                    return CreateStatusResponse("ERROR: No credential information found in envList");
                }

                // Extract username - common across all handlers
                string? username = extraction.EnvList
                        .Where(x => x.env.ToLower().Contains("username"))
                        .FirstOrDefault()?.value?.ToString();

                if (string.IsNullOrEmpty(username))
                {
                    _logger.LogWarning("No username found in envList for {CredentialType}. " +
                        "This may be normal for custom/blank credential types.", CredentialType);
                }
                // Check if user exists before attempting deletion
                var userExists = await CheckUserExistAsync(username);
                if (!userExists)
                {
                    sw.Stop();
                    return CreateStatusResponse($"INFO: User {username} does not exist in {CredentialType} DB, skipping deletion");
                }
                
                _logger.LogInformation("Attempting to delete {CredentialType} credentials" +
                    (string.IsNullOrEmpty(username) ? " (no external user)" : " for user: {Username}"),
                    CredentialType, username);

                // Call derived class to perform specific deletion
                var deleteResult = await DeleteUserAsync(username, cancellationToken);

                if (!deleteResult)
                {
                    _logger.LogError("Failed to delete {CredentialType}" +
                        (string.IsNullOrEmpty(username) ? "" : " user {Username}"),
                        CredentialType, username);
                    return CreateStatusResponse($"ERROR: Failed to delete {CredentialType} credentials");
                }

                _logger.LogInformation("Successfully deleted {CredentialType} credentials" +
                    (string.IsNullOrEmpty(username) ? "" : " for user: {Username}"),
                    CredentialType, username);

                // Handle vault cleanup
                await HandleVaultOperationsAsync(extraction.SubmissionId, cancellationToken);

                sw.Stop();
                _logger.LogInformation("{HandlerName} took {Seconds} seconds",
                    GetType().Name, sw.Elapsed.TotalSeconds);

                string successMessage = $"OK: {CredentialType} credentials deleted successfully";
                if (!string.IsNullOrEmpty(username))
                {
                    successMessage += $" (user '{username}')";
                }
                successMessage += ".";
                return CreateStatusResponse(successMessage);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in {HandlerName}: {Message}",
                    GetType().Name, ex.Message);

                sw.Stop();
                _logger.LogInformation("{HandlerName} took {Seconds} seconds",
                    GetType().Name, sw.Elapsed.TotalSeconds);

                return CreateStatusResponse($"ERROR: Unexpected error in {CredentialType} handler: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Data transfer object for extracted credentials from Zeebe job for delete operations
    /// </summary>
    public class DeleteCredentialExtraction
    {
        public Dictionary<string, object> Variables { get; set; }
        public List<CredentialsCamundaOutput> EnvList { get; set; }
        public string? SubmissionId { get; set; }
        public string? Project { get; set; }
        public string? User { get; set; }
    }
}
