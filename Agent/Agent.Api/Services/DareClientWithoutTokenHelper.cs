using Agent.Api.Repositories.DbContexts;
using FiveSafesTes.Core.Models;
using FiveSafesTes.Core.Models.Enums;
using FiveSafesTes.Core.Models.Settings;
using FiveSafesTes.Core.Services;
using Microsoft.Extensions.Options;

namespace Agent.Api.Services
{
    public class DareClientWithoutTokenHelper : BaseClientHelper, IDareClientWithoutTokenHelper
    {
        private readonly SubmissionKeyCloakSettings _keycloakSettings;
        private readonly ApiEndpointSettings _apiEndpointSettings;
        public DareClientWithoutTokenHelper(IHttpClientFactory httpClientFactory,
            IHttpContextAccessor httpContextAccessor, IConfiguration config, ApplicationDbContext db,
            IOptionsMonitor<SubmissionKeyCloakSettings> keycloakSettings, IOptions<ApiEndpointSettings> apiEndpointSettings) : base(httpClientFactory, httpContextAccessor,apiEndpointSettings.Value.SubmissionApiUrl
            , false)
        {
          _apiEndpointSettings = apiEndpointSettings.Value;
          _keycloakSettings = keycloakSettings.CurrentValue;

            bool useServiceAccount = _keycloakSettings.ConfigInputMethod == ConfigInputMethod.Upload;
            var keycloakDemoMode = KeycloakCommon.ResolveKeycloakDemoMode(_keycloakSettings.KeycloakDemoMode, config["KeycloakDemoMode"]);

            _keycloakTokenHelper = new KeycloakTokenHelper(_keycloakSettings.BaseUrl, _keycloakSettings.ClientId, _keycloakSettings.ClientSecret, _keycloakSettings.Proxy, _keycloakSettings.ProxyAddresURL, keycloakDemoMode, useServiceAccount);

            if (CheckCredsAreAvailable())
            {
                _username = _keycloakSettings.Username;
                _password = _keycloakSettings.PasswordEnc;
                _requiredRole = "dare-tre-admin";
            }
            //Log.Information("{Function} Creds are there? {Creds} with username {Username}, Password {Password} and role {Role}", "DareClientWithoutTokenHelper", _username, _password, _requiredRole);
        }

        public bool CheckCredsAreAvailable()
        {
            return !string.IsNullOrEmpty(_keycloakSettings.Username) && !string.IsNullOrEmpty(_keycloakSettings.PasswordEnc);
        }
    }
}
