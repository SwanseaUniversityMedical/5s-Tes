using System.Text.Json;
using Agent.Api.Repositories.DbContexts;
using Agent.Api.Services;
using FiveSafesTes.Core.Models;
using FiveSafesTes.Core.Models.APISimpleTypeReturns;
using FiveSafesTes.Core.Models.Enums;
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

        private readonly ApplicationDbContext _DbContext;
        private readonly IEncDecHelper _encDecHelper;
        private readonly IConfigurationService _configurationService;
        private readonly VaultConfigurationProvider _vaultConfigProvider;
        private readonly DataEgressKeyCloakSettings _keycloakSettings;
        public KeycloakTokenHelper _keycloakTokenHelper { get; set; }
        

        public DataEgressCredentialsController(ApplicationDbContext applicationDbContext, IEncDecHelper encDec, IConfigurationService configService,
            IOptionsMonitor<DataEgressKeyCloakSettings> keycloakSettings, IConfiguration config)
        {
            _encDecHelper = encDec;
            _DbContext = applicationDbContext;
            _keycloakSettings = keycloakSettings.CurrentValue;
            _keycloakTokenHelper = new KeycloakTokenHelper(_keycloakSettings.BaseUrl, _keycloakSettings.ClientId,
                _keycloakSettings.ClientSecret, _keycloakSettings.Proxy, _keycloakSettings.ProxyAddresURL, _keycloakSettings.KeycloakDemoMode);
            _vaultConfigProvider = ((IConfigurationRoot)config).Providers.OfType<VaultConfigurationProvider>().FirstOrDefault();
        }

        [Authorize(Roles = "dare-tre-admin")]
        [HttpGet("CheckCredentialsAreValid")]
        public async Task<BoolReturn> CheckCredentialsAreValidAsync()
        {
            try
            {
                var result = new BoolReturn() { Result = false };

                if (!string.IsNullOrEmpty(_keycloakSettings.Username) && !string.IsNullOrEmpty(_keycloakSettings.PasswordEnc))
                {

                    var token = await _keycloakTokenHelper.GetTokenForUser(_keycloakSettings.Username, _encDecHelper.Decrypt(_keycloakSettings.PasswordEnc), "dare-tre-admin");
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

            object credsToSave = new
            {
                Username = creds.UserName,
                PasswordEnc = _encDecHelper.Encrypt(creds.PasswordEnc),
                ConfigInputMethod = ConfigInputMethod.Manual
            };

            await _configurationService.AddConfigurationToVault(JsonSerializer.Serialize(credsToSave), nameof(DataEgressKeyCloakSettings));

            // Reload config to apply updated credentials immediately.
            await _vaultConfigProvider.LoadAsync(bypassConfigCheck: true);

            return creds;
        }
    }
}
