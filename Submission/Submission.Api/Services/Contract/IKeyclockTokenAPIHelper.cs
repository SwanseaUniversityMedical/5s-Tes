namespace Submission.Api.Services.Contract
{
    public interface IKeycloakTokenApiHelper
    {
        Task<string> GetTokenForUser(string username, string password, string requiredRole);
    }
}