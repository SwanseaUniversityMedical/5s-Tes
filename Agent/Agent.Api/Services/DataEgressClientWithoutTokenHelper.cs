using Agent.Api.Repositories.DbContexts;
using FiveSafesTes.Core.Models;
using FiveSafesTes.Core.Models.Settings;
using FiveSafesTes.Core.Services;
using Microsoft.Extensions.Options;

namespace Agent.Api.Services
{
    public class DataEgressClientWithoutTokenHelper : BaseClientHelper, IDataEgressClientWithoutTokenHelper
    {
        public readonly DataEgressKeyCloakSettings _keycloakSettings;

        public DataEgressClientWithoutTokenHelper(IHttpClientFactory httpClientFactory,
            IHttpContextAccessor httpContextAccessor, IConfiguration config, ApplicationDbContext db,
            IEncDecHelper encDec, IOptionsMonitor<DataEgressKeyCloakSettings> settings) : base(httpClientFactory, httpContextAccessor,
            config["DataEgressAPISettings:Address"], false)
        {
            _keycloakSettings = settings.CurrentValue;
            _keycloakTokenHelper = new KeycloakTokenHelper(_keycloakSettings.BaseUrl, _keycloakSettings.ClientId, _keycloakSettings.ClientSecret, _keycloakSettings.Proxy, _keycloakSettings.ProxyAddresURL, _keycloakSettings.KeycloakDemoMode);

            if (CheckCredsAreAvailable())
            {
                _username = _keycloakSettings.Username;
                _password = encDec.Decrypt(_keycloakSettings.PasswordEnc);
                _requiredRole = "dare-tre-admin";
            }


        }

        public bool CheckCredsAreAvailable()
        {
            return !string.IsNullOrEmpty(_keycloakSettings.Username) && !string.IsNullOrEmpty(_keycloakSettings.PasswordEnc);
        }
    }
}
