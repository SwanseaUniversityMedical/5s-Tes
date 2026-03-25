using Serilog;
using FiveSafesTes.Core.Models.Settings;
using FiveSafesTes.Core.Services;
using Submission.Api.Services.Contract;

namespace Submission.Api.Services
{
    public class KeycloakTokenApiHelper : IKeycloakTokenApiHelper
    {
        public SubmissionKeyCloakSettings _settings { get; set; }


        public KeycloakTokenApiHelper(SubmissionKeyCloakSettings settings)
        {
            _settings = settings;
        }
        public async Task<string> GetTokenForUser(string username, string password, string requiredRole)
        {
            string keycloakBaseUrl = _settings.BaseUrl;
            string clientId = _settings.ClientId;
            string clientSecret = _settings.ClientSecret;
            var proxyhandler = _settings.getProxyHandler;
            
            
            Log.Information("{Function}} 1 using proxyhandler _settings.Authority > {Authority}, KeycloakDemoMode {KeyCloakDemoMode}", "GetTokenForUser", _settings.Authority, _settings.KeycloakDemoMode);
            return (await KeycloakCommon.GetTokenForUserGuts(username, password, requiredRole, proxyhandler, keycloakBaseUrl, clientId, clientSecret, _settings.KeycloakDemoMode)).token;
        }

    }
    
}
