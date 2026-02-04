using Agent.Api.Repositories.DbContexts;
using FiveSafesTes.Core.Models;
using FiveSafesTes.Core.Models.Settings;
using FiveSafesTes.Core.Services;

namespace Agent.Api.Services
{
    public class DataEgressClientWithoutTokenHelper : BaseClientHelper, IDataEgressClientWithoutTokenHelper
    {
        public ApplicationDbContext CredDb { get; set; }

        public DataEgressClientWithoutTokenHelper(IHttpClientFactory httpClientFactory,
            IHttpContextAccessor httpContextAccessor, IConfiguration config, ApplicationDbContext db,
            IEncDecHelper encDec, DataEgressKeyCloakSettings settings) : base(httpClientFactory, httpContextAccessor,
            config["DataEgressAPISettings:Address"], false)
        {
            CredDb = db;
            _keycloakTokenHelper = new KeycloakTokenHelper(settings.BaseUrl, settings.ClientId, settings.ClientSecret, settings.Proxy, settings.ProxyAddresURL, settings.KeycloakDemoMode);

            var creds = db.KeycloakCredentials.FirstOrDefault(x => x.CredentialType == CredentialType.Egress);
            if (creds != null)
            {
                _username = creds.UserName;
                _password = encDec.Decrypt(creds.PasswordEnc);
                _requiredRole = "dare-tre-admin";
            }


        }

        public bool CheckCredsAreAvailable()
        {
            return CredDb.KeycloakCredentials.Any(x => x.CredentialType == CredentialType.Egress);
        }
    }
}
