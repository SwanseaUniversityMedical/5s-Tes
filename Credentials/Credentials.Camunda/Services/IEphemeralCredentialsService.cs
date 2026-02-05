namespace Credentials.Camunda.Services
{
    public interface IEphemeralCredentialsService
    {
        Task<bool> UpdateCredentialExpirationAsync(string vaultPath, CancellationToken cancellationToken = default);
        Task<string?> GetVaultPathBySubmissionIdAsync(int submissionId, string credentialType, CancellationToken cancellationToken = default);
    }
}
