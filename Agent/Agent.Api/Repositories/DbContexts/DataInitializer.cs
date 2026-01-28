using FiveSafesTes.Core.Models;
using FiveSafesTes.Core.Services;
using Serilog;

namespace Agent.Api.Repositories.DbContexts
{
    public class DataInitaliser
    {

        private readonly ApplicationDbContext _dbContext;
        public IEncDecHelper _encDecHelper { get; set; }

        public DataInitaliser(ApplicationDbContext dbContext, IEncDecHelper encDec)
        {

            _dbContext = dbContext;
            _encDecHelper = encDec;


        }

        public void SeedAllInOneData(string password)
        {
            
            try
            {
                if (!_dbContext.KeycloakCredentials.Any(x => x.CredentialType == CredentialType.Submission))
                {


                    _dbContext.KeycloakCredentials.Add(new KeycloakCredentials()
                    {
                        UserName = "accessfromtretosubmission",
                        CredentialType = CredentialType.Submission,
                        PasswordEnc = _encDecHelper.Encrypt(password)
                    });
                    _dbContext.SaveChanges();
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

                if (!_dbContext.KeycloakCredentials.Any(x => x.CredentialType == CredentialType.Egress))
                {


                    _dbContext.KeycloakCredentials.Add(new KeycloakCredentials()
                    {
                        UserName = "accessfromtretoegress",
                        CredentialType = CredentialType.Egress,
                        PasswordEnc = _encDecHelper.Encrypt(password)
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
