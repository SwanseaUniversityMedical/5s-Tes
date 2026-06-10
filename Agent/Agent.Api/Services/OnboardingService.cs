using System.Net.Http.Headers;
using System.Text.Json;
using FiveSafesTes.Core.Models.Enums;
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
    private readonly IConfiguration _configuration;
    private readonly IEncDecHelper _encDecHelper;
    private readonly IServiceProvider _serviceProvider;
    private readonly JobSettings _jobSettings;
    private readonly VaultConfigurationProvider _vaultConfigProvider;

    private readonly string submissionAddress;

    public OnboardingService(IConfiguration config, IConfigurationService configService, IOptionsMonitor<TreOnboardingConfig> configSettings, 
        JobSettings jobSettings, IEncDecHelper encDec, IServiceProvider serviceProvider)
    {
        _configuration = config;
        _configurationService = configService;
        _onboardingConfig = configSettings;
        _jobSettings = jobSettings;
        _vaultConfigProvider = ((IConfigurationRoot)config).Providers.OfType<VaultConfigurationProvider>().FirstOrDefault();
        _encDecHelper = encDec;
        _serviceProvider = serviceProvider;
        submissionAddress = config["DareAPISettings:Address"];
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

        // Update configuration again to apply new vault changes
        await _vaultConfigProvider.LoadAsync();

        SyncWithSubmission();
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
                var keycloakDemoMode = string.Equals(_configuration["KeycloakDemoMode"], "true", StringComparison.OrdinalIgnoreCase);
                var documentRetriever = new HttpDocumentRetriever { RequireHttps = !keycloakDemoMode };
                ConfigurationManager<OpenIdConnectConfiguration> configManager = new(
                    keycloakSettingsURL,
                    new OpenIdConnectConfigurationRetriever(),
                    documentRetriever);
                OpenIdConnectConfiguration config = await configManager.GetConfigurationAsync();

                // Extract desired values from the retrieved OpenId configuration...
                object keycloakConfig = new
                {
                    Authority = config.Issuer,
                    BaseUrl = config.Issuer,
                    KeycloakDemoMode = keycloakDemoMode
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
        if (string.IsNullOrEmpty(_onboardingConfig.CurrentValue.SubmissionURL))
        {
            Log.Error("OnboardingService:LogIntoSubmissionlayer - SumbissionURL is missing");
        }

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
                    credentials.ClientSecret,
                    Username = credentials.ClientId,
                    PasswordEnc = _encDecHelper.Encrypt(credentials.ClientSecret),
                    ConfigInputMethod = ConfigInputMethod.Upload
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
    public void RestartHangfireJobs()
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

    /// <summary>
    /// Sync the submission layer with the TRE.
    /// </summary>
    public void SyncWithSubmission()
    {
        using (var scope = _serviceProvider.CreateScope())
        {
            var dareSyncHelper = scope.ServiceProvider.GetRequiredService<IDareSyncHelper>();
            var result = dareSyncHelper.SyncSubmissionWithTre().Result;
        }
    }

    /// <summary>
    /// Determines whether we have uploaded our configuration.
    /// </summary>
    /// <returns>Returns true if config has been uploaded.</returns>
    public bool IsConfigurationUploaded()
    {
        return _onboardingConfig.CurrentValue.IsConfigurationImported;
    }

    /// <summary>
    /// Determines whether we are able to sync with the submission layer.
    /// </summary>
    /// <returns>Returns true if we are able to reach the submission layer.</returns>
    public bool IsTRESynced()
    {
        if (string.IsNullOrEmpty(submissionAddress))
        {
            return false;
        }

        try
        {
            using HttpClient client = new();
            HttpResponseMessage response = client.GetAsync(submissionAddress + "/v1/get_test_tes").Result;
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "{Function} Could not reach Submission at {Url}", "IsTRESynced", submissionAddress);
            return false;
        }
    }
}
