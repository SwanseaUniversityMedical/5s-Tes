using System.Text.Json;
using Agent.Api.Models;
using FiveSafesTes.Core.Models.Settings;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Serilog;

namespace Agent.Api.Services;

public class OnboardingService : IOnboardingService
{
    private readonly IOptionsMonitor<VaultConfigSettings> _vaultConfigSettings;
    private readonly IConfigurationService _configurationService;

    public OnboardingService(IConfigurationService configService, IOptionsMonitor<VaultConfigSettings> configSettings)
    {
        _configurationService = configService;
        _vaultConfigSettings = configSettings;
    }

    /// <summary>
    /// Retrieve the submission layer OpenID configuration and add the appropriate values to Vault.
    /// </summary>
    public async Task AddKeycloakSettingsToVault()
    {
        if (!string.IsNullOrEmpty(_vaultConfigSettings.CurrentValue.KeycloakRealmSettingURL))
        {
            try
            {
                ConfigurationManager<OpenIdConnectConfiguration> configManager = new(_vaultConfigSettings.CurrentValue.KeycloakRealmSettingURL, new OpenIdConnectConfigurationRetriever());
                OpenIdConnectConfiguration config = await configManager.GetConfigurationAsync();

                var keycloakConfig = new
                {
                    Authority = config.Issuer,
                    BaseUrl = config.Issuer
                };

                await _configurationService.AddConfigurationToVault(JsonSerializer.Serialize(keycloakConfig), nameof(SubmissionKeyCloakSettings));
            }
            catch (Exception ex)
            {
                Log.Error("OnboardingService:AddKeycloakSettingsToVault - " + ex.Message);
            }
        }
        else
        {
            Log.Error("OnboardingService:AddKeycloakSettingsToVault - Realm Config URL is missing.");
        }
    }
}
