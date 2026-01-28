using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using Agent.Api.Models;
using Agent.Api.Repositories.DbContexts;
using FiveSafesTes.Core.Models;
using FiveSafesTes.Core.Models.Settings;
using FiveSafesTes.Core.Services;
using Newtonsoft.Json;
using Serilog;

namespace Agent.Api.Services
{
    public interface IKeyCloakService
    {
        Task DoGenAccount(string Uers);
        Task DeleteUser(string Uers);

        Task<KeyCloakService.TokenResponse> GenAccessTokenSimple(string adminName, string adminPassword, string max_age);
    }

    public class KeyCloakService : IKeyCloakService
    {


        private readonly ApplicationDbContext _DbContext;
        protected readonly IHttpContextAccessor _httpContextAccessor;

        public IDareSyncHelper _dareSyncHelper { get; set; }
        public readonly TreKeyCloakSettings _TreKeyCloakSettings;
        private readonly IEncDecHelper _encDecHelper;

        public KeyCloakService(IDareSyncHelper dareSyncHelper, ApplicationDbContext applicationDbContext,
            IHttpContextAccessor httpContextAccessor, TreKeyCloakSettings TreKeyCloakSettings, IEncDecHelper encDecHelper)
        {
            _dareSyncHelper = dareSyncHelper;
            _DbContext = applicationDbContext;
            _httpContextAccessor = httpContextAccessor;
            _TreKeyCloakSettings = TreKeyCloakSettings;
            _encDecHelper = encDecHelper;
        }

        public async Task DoGenAccount(string Uers)
        {

            Random RNG = new Random();


            string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz1234567890";
            var pass = "";
            if (string.IsNullOrEmpty(pass))
            {
                pass = new string(Enumerable.Repeat(chars, 25).Select(s => s[RNG.Next(s.Length)]).ToArray());
            }


            var Usercode = Uers;
            var email = $"{Uers}@chi.swan.ac.uk";
            var data = new GenAccountData() { Email = email, Name = Usercode, Password = pass };
            var Result = await GenAccount(data);
            if (Result)
            {
                _DbContext.ProjectAcount.Add(new ProjectAcount()
                {
                    Name = data.Name,
                    Email = data.Email,
                    Pass = _encDecHelper.Encrypt(data.Password)
                });

                _DbContext.SaveChanges();
            }
          


        }


        public async Task DeleteUser(string Uers)
        {
            var login = _DbContext.ProjectAcount.FirstOrDefault(x => x.Name == Uers);
            if (login != null)
            {
                var domain = _TreKeyCloakSettings.BaseUrl.Replace("/realms/Dare-TRE", "");
                var realm = _TreKeyCloakSettings.Realm;
                var creds = _DbContext.KeycloakCredentials.FirstOrDefault(x => x.CredentialType == CredentialType.Tre);
                var adminName = creds.UserName;

                var adminPassword = creds.PasswordEnc; // _encDecHelper.Decrypt(creds.PasswordEnc);

                // Get an access token from Keycloak using admin credentials
                var token = await GetAccessTokenAsync(domain, realm, adminName, adminPassword, "120");
                var UserId = await GetUserIdAsync(domain, realm, token.access_token, Uers.ToLower());

                await DeleteUserAsync(domain, realm, token.access_token, UserId);
                _DbContext.ProjectAcount.Remove(login);
                _DbContext.SaveChanges();

            }
        }

        public class TokenRoles
        {
            public List<string> roles { get; set; }
        }

        public class GenAccountData
        {
            public string Name { get; set; }
            public string Password { get; set; }   
            public string Email { get; set; }
        }


        public async Task<TokenResponse> GenAccessTokenSimple(string adminName, string adminPassword, string max_age)
        {
            var domain = _TreKeyCloakSettings.BaseUrl.Replace("/realms/Dare-TRE", "");
            var realm = _TreKeyCloakSettings.Realm;
            return await GetAccessTokenAsync(domain, realm, adminName, adminPassword, max_age);
        }


        public async Task<TokenResponse> GetAccessTokenAsync(string domain, string realm, string adminName, string adminPassword, string max_age)
        {
            var url = "";
            if (domain.Contains("http")) //NOTEEE will Break if you've got http in url and it's old key cloak
            {
                url = $"{domain}/realms/{realm}/protocol/openid-connect/token";
            }
            else
            {
                url = $"http://{domain}/realms/{realm}/protocol/openid-connect/token";
            }

            Log.Information(" GetAccessTokenAsync url >" + url);
            var client = new HttpClient(GetHttpClientHandler());

            var tokenRequestBody = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                {"client_id", "admin-cli"},
                {"username", adminName},
                {"password", adminPassword},
                {"grant_type", "password"},
                {"max_age", max_age } // Set a longer max_age in seconds
            });

            var response = await client.PostAsync(url, tokenRequestBody);


            var content = await response.Content.ReadAsStringAsync();
            Log.Information(" GetAccessTokenAsync content >" + content);
            var token = JsonConvert.DeserializeObject<TokenResponse>(content);
            return token;
        }




        public async Task<bool> GenAccount(GenAccountData GenAccountData)
        {
            var domain = _TreKeyCloakSettings.BaseUrl.Replace("/realms/Dare-TRE", "");
            var realm = _TreKeyCloakSettings.Realm;
            var creds = _DbContext.KeycloakCredentials.FirstOrDefault(x => x.CredentialType == CredentialType.Tre);

            var adminName = creds.UserName;

            var adminPassword = _encDecHelper.Decrypt(creds.PasswordEnc);


            // Get an access token from Keycloak using admin credentials
            var token = await GetAccessTokenAsync(domain, realm, adminName, adminPassword, "120");
            var didit = await MakeAccounts(domain, realm, token.access_token, GenAccountData);

            if (didit)
            {
                return true;
            }
            else
            {
                return false;
            }
        }


        async Task<string> GetUserIdAsync(string domain, string realm, string accessToken, string username)
        {
            var url = "";
            if (domain.Contains("http")) //NOTEEE will Break if you've got http in url and it's old key cloak
            {
                url = $"{domain}/admin/realms/{realm}/users/?username={username}";
            }
            else
            {
                url = $"http://{domain}/auth/realms/{realm}/protocol/openid-connect/token";
            }

            Log.Information(" GetUserIdAsync url >" + url);
            var client = new HttpClient(GetHttpClientHandler());
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            var response = await client.GetAsync(url);
            var content = await response.Content.ReadAsStringAsync();
            Log.Information(" GetUserIdAsync content >" + content);
            var users = JsonConvert.DeserializeObject<List<User>>(content);
            return users.Count > 0 ? users[0].Id : null;
        }


        async Task DeleteUserAsync(string domain, string realm, string accessToken, string userId)
        {
            var url = "";
            if (domain.Contains("http")) //NOTEEE will Break if you've got http in url and it's old key cloak
            {
                url = $"{domain}/admin/realms/{realm}/users/{userId}";
            }
            else
            {
                url = $"http://{domain}/admin/realms/{realm}/users/{userId}";
            }

            var client = new HttpClient(GetHttpClientHandler());
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            var response = await client.DeleteAsync(url);
            Log.Information(" DeleteUserAsync content >" + await response.Content.ReadAsStringAsync());
            response.EnsureSuccessStatusCode();
        }

        async Task<bool> MakeAccounts(string domain, string realm, string accessToken, GenAccountData GenAccountData)
        {
            var url = "";
            if (domain.Contains("http"))
            {
                url = $"{domain}/admin/realms/{realm}/users";
            }
            else
            {
                url = $"http://{domain}/auth/admin/realms/{realm}/users";
            }

            Log.Information(" MakeAccounts url >" + url);
            var client = new HttpClient(GetHttpClientHandler());
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
 

            string input = GenAccountData.Name;
            string pattern = @"[^a-zA-Z0-9]"; // exclude everything but letters and numbers
            string result = Regex.Replace(input, pattern, "");
            GenAccountData.Name = result;

            NewUserData user = new NewUserData
            {
                firstName = "",
                lastName = "",
                email = GenAccountData.Email,
                enabled = "true",
                username = GenAccountData.Name
            };

            user.credentials.Add(new Credentials()
            {
                value = GenAccountData.Password,
                temporary = "false",
            });

 

            string jsonBody = JsonConvert.SerializeObject(user);

            using (HttpContent content = new StringContent(jsonBody, Encoding.UTF8, "application/json"))
            {
                HttpResponseMessage response = await client.PostAsync(url, content);

                var Contentdata = await response.Content.ReadAsStringAsync();

                Log.Information(" MakeAccounts content >" + Contentdata);
                if (response.IsSuccessStatusCode)
                {
                    Log.Information("User created successfully.");
                    return true;
                }
                else
                {
                    Log.Information($"Failed to create user. Status code: {response.StatusCode}");
                    return false;
                }
            }
        }


        public HttpClientHandler GetHttpClientHandler()
        {
            var handler = new HttpClientHandler();

            if (_TreKeyCloakSettings.Proxy)
            {
                handler.Proxy = new WebProxy()
                {
                    Address = new Uri(_TreKeyCloakSettings.ProxyAddresURL),
                    BypassList = new[] { _TreKeyCloakSettings.BypassProxy }
                };
                handler.UseProxy = true;
            }


            return handler;
        }

        public class TokenResponse
        {
            public string access_token { get; set; }
        }

        class User
        {
            public string Id { get; set; }
        }


        class NewUserData
        {
            public string firstName { get; set; }
            public string lastName { get; set; }
            public string email { get; set; }
            public string enabled { get; set; }
            public string username { get; set; }
            public List<Credentials> credentials { get; set; } = new List<Credentials>();

            public List<string> requiredActions { get; set; } = new List<string>();
        }



        class Credentials
        {
            public string type { get; set; } = "password";
            public string value { get; set; } = "test123";

            public string temporary { get; set; } = "true";
        }

    }
}
