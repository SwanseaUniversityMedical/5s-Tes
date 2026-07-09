using System.Text.Json;
using Agent.Api.Repositories.DbContexts;
using Agent.Api.Services;
using FiveSafesTes.Core.Models;
using FiveSafesTes.Core.Models.Enums;
using FiveSafesTes.Core.Models.Settings;
using FiveSafesTes.Core.Models.ViewModels;
using Hangfire;
using Microsoft.Extensions.Options;

namespace Agent.Api
{
    public interface IDoSyncWork
    {
        [AutomaticRetry(Attempts = 0)]
        Task Execute();
    }

    public class DoSyncWork : IDoSyncWork
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IOptionsMonitor<TreOnboardingConfig> _onboardingConfig;
        private readonly IConfigurationService _configurationService;
        private readonly ApplicationDbContext _dbContext;
        private readonly VaultConfigurationProvider _vaultConfigProvider;

        public DoSyncWork(IConfiguration config, IServiceProvider serviceProvider, IOptionsMonitor<TreOnboardingConfig> configSettings, 
            IConfigurationService configService, ApplicationDbContext dbContext)
        {
            _serviceProvider = serviceProvider;
            _onboardingConfig = configSettings;
            _configurationService = configService;
            _dbContext = dbContext;
            _vaultConfigProvider = ((IConfigurationRoot)config).Providers.OfType<VaultConfigurationProvider>().FirstOrDefault();
        }

        public async Task Execute()
        {
            // Don't execute if no credentials have been uploaded...
            if (!_onboardingConfig.CurrentValue.IsConfigurationImported)
            {
                // ... unless any exist already in the database.
                bool importedCredsFromDb = await TryImportConfigFromDatabase();
                if (!importedCredsFromDb) return;
            }

            using (var scope = _serviceProvider.CreateScope())
            {
                var dareSyncHelper = scope.ServiceProvider.GetRequiredService<IDareSyncHelper>();
                var result = dareSyncHelper.SyncSubmissionWithTre().Result;
            }
        }

        /// <summary>
        /// Credentials may already exist in the database but yet be in vault. If this is the case, they are moved.
        /// </summary>
        private async Task<bool> TryImportConfigFromDatabase()
        {
            KeycloakCredentials? creds = _dbContext.KeycloakCredentials.FirstOrDefault(x => x.CredentialType == CredentialType.Submission);

            if (creds == null)
            {
                // No prior creds exist in the database.
                return false;
            }
            else
            {
                try
                {
                    // Credentials found in the database, move them to vault.
                    object credsToSave = new
                    {
                        Username = creds.UserName,
                        creds.PasswordEnc,
                        ConfigInputMethod = ConfigInputMethod.Manual
                    };

                    object uploadDataToSave = new { IsConfigurationImported = true };

                    await _configurationService.AddConfigurationToVault(JsonSerializer.Serialize(credsToSave), nameof(SubmissionKeyCloakSettings));
                    await _configurationService.AddConfigurationToVault(JsonSerializer.Serialize(uploadDataToSave), nameof(TreOnboardingConfig));

                    // Reload config to apply updated credentials immediately.
                    await _vaultConfigProvider.LoadAsync();

                    // We no longer need these credentials in the database.
                    _dbContext.KeycloakCredentials.Remove(creds);
                    await _dbContext.SaveChangesAsync();

                    return true;
                }
                catch (Exception ex)
                {
                    return false;
                }
            }
        }
    }
}
