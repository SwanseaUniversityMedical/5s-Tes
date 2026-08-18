using System.Text.Json;
using Agent.Api.Services;
using FiveSafesTes.Core.Models;
using FiveSafesTes.Core.Models.Settings;
using FiveSafesTes.Core.Models.ViewModels;
using FiveSafesTes.Core.Services;
using Microsoft.Extensions.Options;
using Serilog;

namespace Agent.Api.Repositories.DbContexts
{
  public class DataInitaliser
  {
    private readonly ApplicationDbContext _dbContext;
    private readonly SubmissionKeyCloakSettings _submissionKeycloakSettings;
    private readonly DataEgressKeyCloakSettings _egressKeycloakSettings;
    private readonly IConfigurationService _configurationService;
    private VaultConfigurationProvider _vaultConfigProvider;
    public IEncDecHelper _encDecHelper { get; set; }

    public DataInitaliser(ApplicationDbContext dbContext, IEncDecHelper encDec,
      IOptions<SubmissionKeyCloakSettings> submissionKeycloakSettings,
      IOptions<DataEgressKeyCloakSettings> egressKeycloakSettings, IConfigurationService configService,
      IConfiguration configuration)
    {
      _dbContext = dbContext;
      _encDecHelper = encDec;
      _submissionKeycloakSettings = submissionKeycloakSettings.Value;
      _egressKeycloakSettings = egressKeycloakSettings.Value;
      _configurationService = configService;
      _vaultConfigProvider = ((IConfigurationRoot)configuration).Providers.OfType<VaultConfigurationProvider>()
        .FirstOrDefault();
    }

    public async Task SeedDemoData(string password)
    {
      try
      {
        string submissionUsername = _submissionKeycloakSettings.Username;
        string submissionPassword = _submissionKeycloakSettings.PasswordEnc;

        if (string.IsNullOrEmpty(submissionUsername) || string.IsNullOrEmpty(submissionPassword))
        {
          object credsToSave = new
          {
            Username = "accessfromtretosubmission",
            PasswordEnc = _encDecHelper.Encrypt(password)
          };

          await _configurationService.AddConfigurationToVault(JsonSerializer.Serialize(credsToSave),
            nameof(SubmissionKeyCloakSettings));
        }

        if (!_dbContext.KeycloakCredentials.Any(x => x.CredentialType == CredentialType.Tre))
        {
          _dbContext.KeycloakCredentials.Add(new KeycloakCredentials()
          {
            UserName = "globaladminuser",
            CredentialType = CredentialType.Tre,
            PasswordEnc = _encDecHelper.Encrypt(password)
          });
          _dbContext.SaveChanges();
        }

        string egressUsername = _egressKeycloakSettings.Username;
        string egressPassword = _egressKeycloakSettings.PasswordEnc;

        if (string.IsNullOrEmpty(egressUsername) || string.IsNullOrEmpty(egressPassword))
        {
          object credsToSave = new
          {
            Username = "accessfromtretoegress",
            PasswordEnc = _encDecHelper.Encrypt(password)
          };

          await _configurationService.AddConfigurationToVault(JsonSerializer.Serialize(credsToSave),
            nameof(DataEgressKeyCloakSettings));
        }
        object configurationFlag = new { IsConfigurationImported = true };
        await _configurationService.AddConfigurationToVault(JsonSerializer.Serialize(configurationFlag),
          nameof(TreOnboardingConfig));
        
        // Refresh configuration with new values
        await _vaultConfigProvider.LoadAsync();
      }
      catch (Exception e)
      {
        Log.Error(e, "{Function} Error seeding data", "SeedData");
        throw;
      }
    }
  }
}
