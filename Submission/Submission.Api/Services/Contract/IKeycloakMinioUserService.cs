namespace Submission.Api.Services.Contract
{
    public interface IKeycloakMinioUserService
    {
        Task<bool> SetMinioUserAttribute(string accessToken, string userName, string attributeName, string NewAttribute);
        Task<bool> RemoveMinioUserAttribute(string accessToken, string userName, string attributeName, string NewAttribute);
        Task<string> GetUserIDAsync(string accessToken, string userName);
    }
}

