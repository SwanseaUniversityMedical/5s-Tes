using System.Text.Json;
using Agent.Api.Repositories.DbContexts;
using Agent.Api.Services;
using FiveSafesTes.Core.Models;
using FiveSafesTes.Core.Models.APISimpleTypeReturns;
using FiveSafesTes.Core.Models.Enums;
using FiveSafesTes.Core.Models.Settings;
using FiveSafesTes.Core.Models.ViewModels;
using FiveSafesTes.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Serilog;

namespace Agent.Api.Controllers
{
    [Route("api/[controller]")]
    
    [ApiController]
    public class SubmissionCredentialsController : Controller
    {
        private readonly IEncDecHelper _encDecHelper;
        private readonly KeycloakTokenHelper _keycloakTokenHelper;
        private readonly IConfigurationService _configurationService;
        private readonly VaultConfigurationProvider _vaultConfigProvider;
        private readonly SubmissionKeyCloakSettings _keycloakSettings;

        public SubmissionCredentialsController(IConfiguration config, IEncDecHelper encDec, IOptionsMonitor<SubmissionKeyCloakSettings> settings, IConfigurationService configurationService)
        {
            _keycloakSettings = settings.CurrentValue;

            _encDecHelper = encDec;
            _keycloakTokenHelper = new KeycloakTokenHelper(_keycloakSettings.BaseUrl, _keycloakSettings.ClientId,
                _keycloakSettings.ClientSecret, _keycloakSettings.Proxy, _keycloakSettings.ProxyAddresURL, _keycloakSettings.KeycloakDemoMode);
            _configurationService = configurationService;
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

            object uploadDataToSave = new { IsConfigurationImported = true };

            await _configurationService.AddConfigurationToVault(JsonSerializer.Serialize(credsToSave), nameof(SubmissionKeyCloakSettings));
            await _configurationService.AddConfigurationToVault(JsonSerializer.Serialize(uploadDataToSave), nameof(TreOnboardingConfig));

            // Reload config to apply updated credentials immediately.
            await _vaultConfigProvider.LoadAsync(bypassConfigCheck: true);

            return creds;
        }

        [Authorize(Roles = "dare-tre-admin")]
        [HttpPost("WipeVaultCredentials")]
        public async Task<BoolReturn> WipeVaultCredentials()
        {
            // Wipe any previously uploaded keycloak settings from vault.
            await _configurationService.RemoveConfigurationFromVault(nameof(SubmissionKeyCloakSettings));
            await _vaultConfigProvider.LoadAsync(bypassConfigCheck: true);
            return new() { Result = true };
        }
    }
}
