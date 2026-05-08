using System.Net.Http.Headers;
using System.Text.Json;
using FiveSafesTes.Core.Models.Settings;
using FiveSafesTes.Core.Models.ViewModels;
using FiveSafesTes.Core.Services;
using Hangfire;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Serilog;

namespace Agent.Api.Services;

public class OnboardingService : IOnboardingService
{
    private readonly IOptionsMonitor<TreOnboardingConfig> _onboardingConfig;
    private readonly IConfigurationService _configurationService;
    private readonly IDareClientWithoutTokenHelper _clientHelper;
    private readonly JobSettings _jobSettings;
    private readonly VaultConfigurationProvider _vaultConfigProvider;

    public OnboardingService(IConfiguration config, IConfigurationService configService, IOptionsMonitor<TreOnboardingConfig> configSettings,
        IDareClientWithoutTokenHelper clientHelper, JobSettings jobSettings)
    {
        _configurationService = configService;
        _onboardingConfig = configSettings;
        _jobSettings = jobSettings;
        _clientHelper = clientHelper;
        _vaultConfigProvider = ((IConfigurationRoot)config).Providers.OfType<VaultConfigurationProvider>().FirstOrDefault();
    }

    /// <summary>
    /// Reads a JSON config file and applies its values to our own configuration.
    /// </summary>
    /// <param name="file">The JSON file we're reading the values from.</param>
    public async Task UploadJsonConfig(IFormFile file)
    {
        using StreamReader reader = new(file.OpenReadStream());
        string json = await reader.ReadToEndAsync();

        await _configurationService.AddConfigurationToVault(json, nameof(TreOnboardingConfig));

        // Update configuration immediately
        await _vaultConfigProvider.LoadAsync();
        await AddKeycloakSettingsToVault(_onboardingConfig.CurrentValue.KeycloakRealmSettingURL);

        await LogIntoSubmissionLayer();

        RestartHangfireJobs();
    }

    /// <summary>
    /// Retrieve the submission layer OpenID configuration and add the appropriate values to Vault.
    /// </summary>
    private async Task AddKeycloakSettingsToVault(string keycloakSettingsURL)
    {
        if (!string.IsNullOrEmpty(keycloakSettingsURL))
        {
            try
            {
                ConfigurationManager<OpenIdConnectConfiguration> configManager = new(keycloakSettingsURL, new OpenIdConnectConfigurationRetriever());
                OpenIdConnectConfiguration config = await configManager.GetConfigurationAsync();

                // Extract desired values from the retrieved OpenId configuration...
                object keycloakConfig = new
                {
                    Authority = config.Issuer,
                    BaseUrl = config.Issuer
                };

                // ... then add them to vault.
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

    /// <summary>
    /// Log into the submission layer using the JWT and add the retrieved credentials to vault.
    /// </summary>
    private async Task LogIntoSubmissionLayer()
    {
        HttpClient httpClient = new();
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _onboardingConfig.CurrentValue.JWT);

        HttpResponseMessage response = await httpClient.PostAsync($"{_onboardingConfig.CurrentValue.SubmissionURL}/api/Onboarding/RetrieveCredentials", null);

        if (response.IsSuccessStatusCode)
        {
            OnboardingCredentialsResponse? credentials = await response.Content.ReadFromJsonAsync<OnboardingCredentialsResponse>();

            if (credentials != null)
            {
                object vaultCredentials = new
                {
                    credentials.ClientId,
                    credentials.ClientSecret
                };

                await _configurationService.AddConfigurationToVault(JsonSerializer.Serialize(vaultCredentials), nameof(SubmissionKeyCloakSettings));
            }
        }
        else
        {
            Log.Error("OnboardingService:LogIntoSubmissionlayer - " + response.StatusCode);
        }
    }

    /// <summary>
    /// The health check hangfire job kills the other jobs if the connection to the API is unhealthy.
    /// These jobs are revived when new config is uploaded, as the updated values can fix previous connection issues.
    /// </summary>
    private void RestartHangfireJobs()
    {
        string syncJobName = _jobSettings.SyncJobName;
        if (_jobSettings.syncSchedule == 0)
        {
            RecurringJob.RemoveIfExists(syncJobName);
        }
        else
        {
            RecurringJob.AddOrUpdate<IDoSyncWork>(syncJobName, x => x.Execute(), Cron.MinuteInterval(_jobSettings.syncSchedule));
        }

        string scanJobName = _jobSettings.ScanJobName;
        if (_jobSettings.scanSchedule == 0)
        {
            RecurringJob.RemoveIfExists(scanJobName);
        }
        else
        {
            RecurringJob.AddOrUpdate<IDoAgentWork>(scanJobName, x => x.Execute(), Cron.MinuteInterval(_jobSettings.scanSchedule));
        }
    }
}
