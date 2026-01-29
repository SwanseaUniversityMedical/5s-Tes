using System.Security.Cryptography;
using System.Text.Json;
using Agent.Api.Models;
using Agent.Api.Repositories.DbContexts;

namespace Agent.Api.Services
{
    public interface IHasuraAuthenticationService
    {
        public string GetNewToken(string role);
        public string CheckeTokenAndGetRoles(string Token);

        public bool ExpirerToken(string Token);
    }

    public class HasuraAuthenticationService : IHasuraAuthenticationService
    {
        private readonly AuthenticationSettings _authenticationSettings;
        private readonly ApplicationDbContext _applicationDbContext;


        public HasuraAuthenticationService(AuthenticationSettings AuthenticationSettings, ApplicationDbContext applicationDbContext)
        {
            _authenticationSettings = AuthenticationSettings;
            _applicationDbContext = applicationDbContext;
        }


        public string GetNewToken(string schemaName)
        {
            //schema name becomes role
            var Token = GenToken();

            _applicationDbContext.DataToRoles.Add(new RoleData()
            {
                Token = Token,
                Name = schemaName,
            });
            _applicationDbContext.SaveChanges();
            return Token;
        }


        public string CheckeTokenAndGetRoles(string Token)
        {
            if (string.IsNullOrEmpty(Token))
            {
                return null;
            }

            var role = _applicationDbContext.DataToRoles.FirstOrDefault(x => x.Token == Token);
            if (role == null) return null;


            if ((DateTime.UtcNow - role.DateTime).Days > _authenticationSettings.TokenExpireDays)
            {
                _applicationDbContext.DataToRoles.Remove(role);
                _applicationDbContext.SaveChanges();
                return null;
            }

            var hasuraVariables = new Dictionary<string, string> {
                        { "x-hasura-role", role.Name },
                        { "x-hasura-user_id", role.Id.ToString() },
                };
            //cool.StatusCode = 200;

            var _jsonSerializerOptions = new JsonSerializerOptions()
            {
                PropertyNameCaseInsensitive = true,
            };

            var dta = System.Text.Json.JsonSerializer.Serialize(hasuraVariables, _jsonSerializerOptions);
            return dta;

        }

        public bool ExpirerToken(string Token)
        {
            var role = _applicationDbContext.DataToRoles.FirstOrDefault(x => x.Token == Token);
            if (role == null) return false;


            _applicationDbContext.DataToRoles.Remove(role);
            _applicationDbContext.SaveChanges();
            return true;

        }

        
        private string GenToken()
        {
            string code = "";
            bool Duplicate = true;
            string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz1234567890";
            using (var rng = new RNGCryptoServiceProvider())
            {

                List<string> usedCodes = new List<string>();
                
                for (int i = 0; i < 3; i++)
                {
                    byte[] randomBytes = new byte[128];
                    rng.GetBytes(randomBytes);
                    code = new string(Enumerable.Range(0, 128)
                        .Select(_ => chars[randomBytes[_] % chars.Length])
                        .ToArray());
                    
                    if (_applicationDbContext.DataToRoles.Any(x => x.Token == code) == false)
                    {
                        Duplicate = false;
                        break;
                    }
                }
            }

            if (Duplicate)
            {
                throw new Exception("Was unable to generate unique code");
            }
            return code;
        }
    }
}
