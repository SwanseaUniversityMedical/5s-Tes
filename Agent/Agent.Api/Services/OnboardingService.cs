using System.Net.Http.Headers;
using System.Text.Json;
using FiveSafesTes.Core.Models.Enums;
using FiveSafesTes.Core.Models.Settings;
using FiveSafesTes.Core.Models.ViewModels;
using FiveSafesTes.Core.Services;
using Hangfire;
using Hangfire.Storage;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Serilog;

namespace Agent.Api.Services;

public class OnboardingService(
  IConfiguration configuration,
  IConfigurationService configService,
  IOptionsMonitor<TreOnboardingConfig> configSettings,
  JobSettings jobSettings,
  IEncDecHelper encDec,
  IServiceProvider serviceProvider,
  IOptions<ApiEndpointSettings> apiEndpoints)
  : IOnboardingService
{
  private readonly VaultConfigurationProvider _vaultConfigProvider = ((IConfigurationRoot)configuration).Providers.OfType<VaultConfigurationProvider>().FirstOrDefault();
    private readonly ApiEndpointSettings _apiEndpoints = apiEndpoints.Value;

    /// <summary>
    /// Reads a JSON config file and applies its values to our own configuration.
    /// </summary>
    /// <param name="file">The JSON file we're reading the values from.</param>
    public async Task UploadJsonConfig(IFormFile file)
    {
        using StreamReader reader = new(file.OpenReadStream());
        string json = await reader.ReadToEndAsync();

        await configService.AddConfigurationToVault(json, nameof(TreOnboardingConfig));

        // Update configuration immediately
        await _vaultConfigProvider.LoadAsync();

        await AddKeycloakSettingsToVault(configSettings.CurrentValue.KeycloakRealmSettingURL);

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
                var keycloakDemoMode = string.Equals(configuration["KeycloakDemoMode"], "true", StringComparison.OrdinalIgnoreCase);
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
                await configService.AddConfigurationToVault(JsonSerializer.Serialize(keycloakConfig), nameof(SubmissionKeyCloakSettings));
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
        if (string.IsNullOrEmpty(configSettings.CurrentValue.SubmissionURL))
        {
            Log.Error("OnboardingService:LogIntoSubmissionlayer - SumbissionURL is missing");
        }

        HttpClient httpClient = new();
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", configSettings.CurrentValue.JWT);

        HttpResponseMessage response = await httpClient.PostAsync($"{configSettings.CurrentValue.SubmissionURL}/api/Onboarding/RetrieveCredentials", null);

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
                    PasswordEnc = encDec.Encrypt(credentials.ClientSecret),
                    ConfigInputMethod = ConfigInputMethod.Upload
                };

                await configService.AddConfigurationToVault(JsonSerializer.Serialize(vaultCredentials), nameof(SubmissionKeyCloakSettings));
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
        string syncJobName = jobSettings.SyncJobName;
        if (jobSettings.syncSchedule == 0)
        {
            RecurringJob.RemoveIfExists(syncJobName);
        }
        else
        {
            RecurringJob.AddOrUpdate<IDoSyncWork>(syncJobName, x => x.Execute(), Cron.MinuteInterval(jobSettings.syncSchedule));
        }

        string scanJobName = jobSettings.ScanJobName;
        if (jobSettings.scanSchedule == 0)
        {
            RecurringJob.RemoveIfExists(scanJobName);
        }
        else
        {
            RecurringJob.AddOrUpdate<IDoAgentWork>(scanJobName, x => x.Execute(), Cron.MinuteInterval(jobSettings.scanSchedule));
        }
    }

    /// <summary>
    /// Sync the submission layer with the TRE.
    /// </summary>
    public void SyncWithSubmission()
    {
        using (var scope = serviceProvider.CreateScope())
        {
            try
            {
                var dareSyncHelper = scope.ServiceProvider.GetRequiredService<IDareSyncHelper>();
                var result = dareSyncHelper.SyncSubmissionWithTre().Result;
            }
            catch (Exception ex)
            {
                Log.Error("OnboardingService:SyncWithSubmission: " + ex.Message);
            }
        }
    }

    /// <summary>
    /// Determines whether we have uploaded our configuration.
    /// </summary>
    /// <returns>Returns true if config has been uploaded.</returns>
    public bool IsConfigurationUploaded()
    {
        return configSettings.CurrentValue.IsConfigurationImported;
    }

    /// <summary>
    /// Determines whether the Sync Hangfire job is currently running.
    /// </summary>
    /// <returns>Returns true if the sync job is present in Hangfire.</returns>
    public bool IsSyncJobCreated()
    {
        List<RecurringJobDto> recurringJobs = JobStorage.Current.GetConnection().GetRecurringJobs();
        return recurringJobs.Any(x => x.Id == jobSettings.SyncJobName);
    }

    /// <summary>
    /// Determines whether we are able to sync with the submission layer.
    /// </summary>
    /// <returns>Returns true if we are able to reach the submission layer.</returns>
    public bool IsTRESynced()
    {
        if (string.IsNullOrEmpty(_apiEndpoints.SubmissionApiUrl))
        {
            return false;
        }

        try
        {
            using HttpClient client = new();
            HttpResponseMessage response = client.GetAsync(_apiEndpoints.SubmissionApiUrl + "/api/HealthCheck/CheckHealth").Result;
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "{Function} Could not reach Submission at {Url}", "IsTRESynced", _apiEndpoints.SubmissionApiUrl);
            return false;
        }
    }
}
