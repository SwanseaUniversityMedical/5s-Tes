namespace Credentials.Camunda.Services
{
    public interface IEphemeralCredentialsService
    {
        Task<bool> UpdateCredentialExpirationAsync(string vaultPath, CancellationToken cancellationToken = default);
        Task<string?> GetVaultPathBySubmissionIdAsync(int submissionId, string credentialType, CancellationToken cancellationToken = default);

        /// <summary>
        /// Every vault path for this submission and credential type. The tre handler writes
        /// one per image code, so a type can own more than one.
        /// </summary>
        Task<List<string>> GetVaultPathsBySubmissionIdAsync(int submissionId, string credentialType, CancellationToken cancellationToken = default);
    }
}
