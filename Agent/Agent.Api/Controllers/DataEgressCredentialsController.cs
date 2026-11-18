using System.Text.Json;
using Agent.Api.Services;
using FiveSafesTes.Core.Models;
using FiveSafesTes.Core.Models.APISimpleTypeReturns;
using FiveSafesTes.Core.Models.Settings;
using FiveSafesTes.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Serilog;

namespace Agent.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class DataEgressCredentialsController : Controller
    {
        private readonly IConfigurationService _configurationService;
        private readonly VaultConfigurationProvider _vaultConfigProvider;
        private readonly DataEgressKeyCloakSettings _keycloakSettings;
        public KeycloakTokenHelper _keycloakTokenHelper { get; set; }
        private readonly EgressSettings _egressSettings;

        public DataEgressCredentialsController(IConfigurationService configService, IOptionsMonitor<DataEgressKeyCloakSettings> keycloakSettings, IConfiguration config,
            EgressSettings egressSettings)
        {
            _egressSettings = egressSettings;
            _configurationService = configService;
            _keycloakSettings = keycloakSettings.CurrentValue;
            var keycloakDemoMode = KeycloakCommon.ResolveKeycloakDemoMode(_keycloakSettings.KeycloakDemoMode, config["KeycloakDemoMode"]);
            _keycloakTokenHelper = new KeycloakTokenHelper(_keycloakSettings.BaseUrl, _keycloakSettings.ClientId,
                _keycloakSettings.ClientSecret, _keycloakSettings.Proxy, _keycloakSettings.ProxyAddresURL, keycloakDemoMode);
            _vaultConfigProvider = ((IConfigurationRoot)config).Providers.OfType<VaultConfigurationProvider>().FirstOrDefault();
        }

        [Authorize(Roles = "dare-tre-admin")]
        [HttpGet("AreCredentialsConfigured")]
        public BoolReturn AreCredentialsConfigured()
        {
            if (_egressSettings.Provider == EgressProvider.Airlock)
                return new BoolReturn { Result = true };

            return new BoolReturn
            {
                Result = !string.IsNullOrEmpty(_keycloakSettings.Username)
                    && !string.IsNullOrEmpty(_keycloakSettings.PasswordEnc)
            };
        }

        [Authorize(Roles = "dare-tre-admin")]
        [HttpGet("CheckCredentialsAreValid")]
        public async Task<BoolReturn> CheckCredentialsAreValidAsync()
        {
            try
            {
                // Agent.Web's home page sends admins to the Data Egress credentials screen when this is false; with Airlock those credentials aren't used, so report them as not needed.
                if (_egressSettings.Provider == EgressProvider.Airlock)
                    return new BoolReturn { Result = true };

                var result = new BoolReturn() { Result = false };

                if (!string.IsNullOrEmpty(_keycloakSettings.Username) && !string.IsNullOrEmpty(_keycloakSettings.PasswordEnc))
                {

                    var token = await _keycloakTokenHelper.GetTokenForUser(_keycloakSettings.Username, _keycloakSettings.PasswordEnc, "dare-tre-admin");
                    result.Result = !string.IsNullOrWhiteSpace(token.token);
                }

                return result;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "{Function} Crash", "CheckCredentialsAreValid");
                throw;
            }
        }

        [Authorize(Roles = "dare-tre-admin")]
        [HttpPost("UpdateCredentials")]
        public async Task<KeycloakCredentials> UpdateCredentials(KeycloakCredentials creds)
        {
            var token = await _keycloakTokenHelper.GetTokenForUser(creds.UserName, creds.PasswordEnc, "dare-tre-admin");

            if (string.IsNullOrWhiteSpace(token.token))
            {
                Log.Information($"UpdateCredentials creds.Valid = false  for {creds.UserName}");
                creds.ErrorMessage = token.Errorstring;
                creds.Valid = false;
                return creds;
            }


            // Serialize the username and password and add them to Vault.
            object credsToSave = new
            {
                Username = creds.UserName,
                PasswordEnc = creds.PasswordEnc
            };

            await _configurationService.AddConfigurationToVault(JsonSerializer.Serialize(credsToSave), nameof(DataEgressKeyCloakSettings));

            // Reload config to apply updated credentials immediately.
            await _vaultConfigProvider.LoadAsync();

            return creds;
        }
    }
}
