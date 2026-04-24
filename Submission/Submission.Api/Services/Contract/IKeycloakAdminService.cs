namespace Submission.Api.Services.Contract
{
    public interface IKeycloakAdminService
    {
        Task<(string clientUuid, string clientId, string clientSecret)> CreateServiceAccountAsync(string treName);
        Task<bool> DeleteServiceAccountAsync(string clientUuid);
    }
}
