using Credentials.Camunda.Services;
using Zeebe.Client.Accelerator.Attributes;

namespace Credentials.Camunda.ProcessHandlers
{
    /// <summary>
    /// Handler for deleting blank/custom credentials from Vault.
    /// Removes stored environment variables that don't fit standard types (Postgres, Trino).
    /// </summary>
    [JobType("delete-tre-credentials")]
    public class DeleteTreCredentialsHandler : DeleteCredentialHandlerBase
    {
        private readonly ILogger<DeleteTreCredentialsHandler> _logger;
        protected override string CredentialType => "tre";

        public DeleteTreCredentialsHandler(
            ILogger<DeleteTreCredentialsHandler> logger,
            IVaultCredentialsService vaultCredentialsService,
            IEphemeralCredentialsService ephemeralCredentialsService)
            : base(logger, vaultCredentialsService, ephemeralCredentialsService)
        {
            _logger = logger;
        }

        /// <summary>
        /// Delete blank credentials from Vault.
        /// For blank credentials, we skip user deletion (there's no external user)
        /// and only remove from Vault.
        /// </summary>
        protected override async Task<bool> DeleteUserAsync(string? username, CancellationToken cancellationToken)
        {
            // For blank credentials, there's no external user to delete
            // We just return true to continue with vault cleanup
            _logger.LogInformation("Blank credentials - no external user to delete, proceeding with vault cleanup");
            return true;
        }
        
        protected override async Task<bool> CheckUserExistAsync(string username)
        {
            // Assume this always returns True until understanding about DeleteTreCredentials is gained 
           return true;
        }
    }
}
