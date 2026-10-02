using Credentials.Models.DbContexts;
using Credentials.Models.Models;
using Microsoft.EntityFrameworkCore;

namespace Credentials.Camunda.Services
{
    public class EphemeralCredentialsService : IEphemeralCredentialsService
    {
        private readonly CredentialsDbContext _credentialsDbContext;
        private readonly ILogger<EphemeralCredentialsService> _logger;

        public EphemeralCredentialsService(
            CredentialsDbContext credentialsDbContext,
            ILogger<EphemeralCredentialsService> logger)
        {
            _credentialsDbContext = credentialsDbContext;
            _logger = logger;
        }

        public async Task<bool> UpdateCredentialExpirationAsync(string vaultPath, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation($"Updating audit trail for vaultPath: {vaultPath}");

                var credential = await _credentialsDbContext.EphemeralCredentials
                    .FirstOrDefaultAsync(c => c.VaultPath == vaultPath && !c.ExpiredAt.HasValue, cancellationToken);

                if (credential != null)
                {
                    credential.ExpiredAt = DateTime.UtcNow;
                    await _credentialsDbContext.SaveChangesAsync(cancellationToken);
                    _logger.LogInformation($"Successfully updated audit trail for vaultPath: {vaultPath}");
                    return true;
                }
                else
                {
                    _logger.LogWarning($"No credential found in database for vaultPath: {vaultPath}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating audit trail for vaultPath: {vaultPath}");
                return false;
            }
        }

        /// <summary>
        /// Every vault path stored for this submission and credential type, newest first.
        ///
        /// The tre handler writes one path per image code, so a single credential type can
        /// now own several. Deleting only the first would leave the rest behind in Vault.
        /// </summary>
        public async Task<List<string>> GetVaultPathsBySubmissionIdAsync(int submissionId, string credentialType, CancellationToken cancellationToken = default)
        {
            try
            {
                return await _credentialsDbContext.EphemeralCredentials
                    .Where(c => c.SubmissionId == submissionId
                        && c.CredentialType == credentialType
                        && !c.ExpiredAt.HasValue
                        && c.SuccessStatus == SuccessStatus.Success
                        && c.VaultPath != null)
                    .OrderByDescending(c => c.CreatedAt)
                    .Select(c => c.VaultPath!)
                    .ToListAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving vaultPaths for submissionId: {SubmissionId}, credentialType: {CredentialType}", submissionId, credentialType);
                return new List<string>();
            }
        }

        public async Task<string?> GetVaultPathBySubmissionIdAsync(int submissionId, string credentialType, CancellationToken cancellationToken = default)
        {
            try
            {
                var credential = await _credentialsDbContext.EphemeralCredentials
                    .Where(c => c.SubmissionId == submissionId
                        && c.CredentialType == credentialType
                        && !c.ExpiredAt.HasValue
                        && c.SuccessStatus == SuccessStatus.Success)
                    .OrderByDescending(c => c.CreatedAt)
                    .FirstOrDefaultAsync(cancellationToken);

                return credential?.VaultPath;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving vaultPath for submissionId: {SubmissionId}, credentialType: {CredentialType}", submissionId, credentialType);
                return null;
            }
        }

    }

}
