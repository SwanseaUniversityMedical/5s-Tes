using System.Text.Json;
using Agent.Api.Services;
using FiveSafesTes.Core.Models;
using FiveSafesTes.Core.Models.Enums;
using FiveSafesTes.Core.Models.Settings;
using FiveSafesTes.Core.Services;
using Microsoft.Extensions.Options;
using Serilog;

namespace Agent.Api.Repositories.DbContexts
{
    public class DataInitaliser
    {

        private readonly ApplicationDbContext _dbContext;
        private readonly IOptionsMonitor<SubmissionKeyCloakSettings> _submissionKeycloakSettings;
        private readonly IOptionsMonitor<DataEgressKeyCloakSettings> _egressKeycloakSettings;
        private readonly IConfigurationService _configurationService;
        public IEncDecHelper _encDecHelper { get; set; }

        public DataInitaliser(ApplicationDbContext dbContext, IEncDecHelper encDec, IOptionsMonitor<SubmissionKeyCloakSettings> submissionKeycloakSettings,
            IOptionsMonitor<DataEgressKeyCloakSettings> egressKeycloakSettings, IConfigurationService configService)
        {

            _dbContext = dbContext;
            _encDecHelper = encDec;
            _submissionKeycloakSettings = submissionKeycloakSettings;
            _egressKeycloakSettings = egressKeycloakSettings;
            _configurationService = configService;
        }

        public void SeedDemoData(string password)
        {
            
            try
            {
                string submissionUsername = _submissionKeycloakSettings.CurrentValue.Username;
                string submissionPassword = _submissionKeycloakSettings.CurrentValue.PasswordEnc;

                if (string.IsNullOrEmpty(submissionUsername) || string.IsNullOrEmpty(submissionPassword))
                {
                    object credsToSave = new
                    {
                        Username = "accessfromtretosubmission",
                        PasswordEnc = _encDecHelper.Encrypt(password)
                    };

                    await _configurationService.AddConfigurationToVault(JsonSerializer.Serialize(credsToSave), nameof(SubmissionKeyCloakSettings));
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

                string egressUsername = _egressKeycloakSettings.CurrentValue.Username;
                string egressPassword = _egressKeycloakSettings.CurrentValue.PasswordEnc;

                if (string.IsNullOrEmpty(egressUsername) || string.IsNullOrEmpty(egressPassword))
                {
                    object credsToSave = new
                    {
                        Username = "accessfromtretoegress",
                        PasswordEnc = _encDecHelper.Encrypt(password)
                    };

                    await _configurationService.AddConfigurationToVault(JsonSerializer.Serialize(credsToSave), nameof(DataEgressKeyCloakSettings));
                }
            }
            catch (Exception e)
            {
                Log.Error(e, "{Function} Error seeding data", "SeedData");
                throw;
            }




        }

        public void SeedData()
        {
            return;
            try
            {
                if (!_dbContext.KeycloakCredentials.Any(x => x.CredentialType == CredentialType.Submission))
                {


                    _dbContext.KeycloakCredentials.Add(new KeycloakCredentials()
                    {
                        UserName = "sailtreapi",
                        CredentialType = CredentialType.Submission,
                        PasswordEnc = _encDecHelper.Encrypt("password123")
                    });
                    _dbContext.SaveChanges();
                }

                if (!_dbContext.KeycloakCredentials.Any(x => x.CredentialType == CredentialType.Egress))
                {


                    _dbContext.KeycloakCredentials.Add(new KeycloakCredentials()
                    {
                        UserName = "sailegressapi",
                        CredentialType = CredentialType.Egress,
                        PasswordEnc = _encDecHelper.Encrypt("password123")
                    });
                    _dbContext.SaveChanges();
                }
            }
            catch (Exception e)
            {
                Log.Error(e, "{Function} Error seeding data", "SeedData");
                throw;
            }




        }
    }
}
