using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Serilog;
using System.IdentityModel.Tokens.Jwt;
using Newtonsoft.Json.Linq;
using System.Net;
using FiveSafesTes.Core.Models.Settings;

namespace Submission.Web.Controllers
{
    [Authorize]
    public class AccountController : Controller
    {
        public SubmissionKeyCloakSettings _keycloakSettings { get; set; }

        public AccountController(SubmissionKeyCloakSettings keycloakSettings)
        {
            _keycloakSettings = keycloakSettings;
        }


        [HttpGet]
        [Authorize]
        public async Task<IActionResult> NewTokenIssue()
        {


            var context = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            var result = "";
            //Refresh tokens are used once the access or ID tokens expire
            var currentAccessToken = context.Properties.GetTokenValue("access_token");
            var currentRefreshToken = context.Properties.GetTokenValue("refresh_token");

            string keycloakBaseUrl = _keycloakSettings.BaseUrl;
            string clientId = _keycloakSettings.ClientId;
            string clientSecret = _keycloakSettings.ClientSecret;
            string refreshToken = currentRefreshToken;

            HttpClientHandler handler = new HttpClientHandler();

            if (_keycloakSettings.Proxy)
            {
                handler = new HttpClientHandler
                {
                    Proxy = new WebProxy(_keycloakSettings.ProxyAddresURL, true), // Replace with your proxy server URL
                    UseProxy = _keycloakSettings.Proxy,
                };
            }

            HttpClient httpClient = new HttpClient(handler);

            var tokenEndpoint = $"{keycloakBaseUrl}/protocol/openid-connect/token";
            var tokenRequestBody = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                {"grant_type", "refresh_token"},
                {"client_id", clientId},
                {"client_secret", clientSecret},
                {"refresh_token", refreshToken},
                {"max_age", _keycloakSettings.TokenRefreshSeconds} // Set a longer max_age in seconds
            });

           

            var tokenResponse = await httpClient.PostAsync(tokenEndpoint, tokenRequestBody);
            var tokenResponseContent = await tokenResponse.Content.ReadAsStringAsync();

            if (tokenResponse.IsSuccessStatusCode)
            {
                var tokenJson = JObject.Parse(tokenResponseContent);
                var newAccessToken = tokenJson["access_token"].ToString();
                result = newAccessToken;
            }
            else
            {
                Log.Error("{Function} Error refreshing token: {tokenResponseContent}", "NewToken", tokenResponseContent);
            }
            ViewBag.AccessToken = result;

            // Decode the JWT to extract the expiration time
            var tokenHandler = new JwtSecurityTokenHandler();
            var jwtToken = tokenHandler.ReadJwtToken(result);
            var expUnix = long.Parse(jwtToken.Claims.First(c => c.Type == "exp").Value);
            var expirationDate = DateTimeOffset.FromUnixTimeSeconds(expUnix).UtcDateTime;
            ViewBag.TokenExpiryDate = expirationDate;
            
            return View();
        }


     
        public class TokenRoles
        {
            public List<string> roles { get; set; }
        }

        public IActionResult Login()
        {
            if (!HttpContext.User.Identity.IsAuthenticated) 
            {
                return Challenge(OpenIdConnectDefaults.AuthenticationScheme);
            }
            return RedirectToAction("LoggedInUser", "Home");
        }

        public IActionResult LoginAfterTokenExpired()
        {
            return new SignOutResult(new[]
            {
                OpenIdConnectDefaults.AuthenticationScheme,
                CookieAuthenticationDefaults.AuthenticationScheme
            }, new AuthenticationProperties
            {
                RedirectUri = Url.Action("Login", "Account")
            });
        }

        public IActionResult Logout()
        {
            return new SignOutResult(new[]
            {
                OpenIdConnectDefaults.AuthenticationScheme,
                CookieAuthenticationDefaults.AuthenticationScheme
            }, new AuthenticationProperties
            {
                RedirectUri = Url.Action("Login", "Account")
            });
        }
        public async Task<IActionResult> AccessDenied(string ReturnUrl)
        {
            var accessToken = await HttpContext.GetTokenAsync("access_token");
            return View();
        }


       

    }
}
