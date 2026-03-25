using Microsoft.AspNetCore.Authentication;
using Newtonsoft.Json.Linq;
using Serilog;
using System.Net;
using FiveSafesTes.Core.Models.Settings;

namespace Submission.Api.Services
{
    public interface IKeyCloakService
    {
        Task<string> RefreshUserToken(AuthenticateResult context);

    }

    public class KeyCloakService : IKeyCloakService
    {

        private readonly SubmissionKeyCloakSettings _SubmissionKeyCloakSettings;

        public KeyCloakService(SubmissionKeyCloakSettings KeyCloakSettings)
        {
            _SubmissionKeyCloakSettings = KeyCloakSettings;
        }

        public async Task<string> RefreshUserToken(AuthenticateResult context)
        {
            var result = "";
            //Refresh tokens are used once the access or ID tokens expire
            var currentAccessToken = context.Properties.GetTokenValue("access_token");
            var currentRefreshToken = context.Properties.GetTokenValue("refresh_token");

            string keycloakBaseUrl = _SubmissionKeyCloakSettings.BaseUrl;
            string clientId = _SubmissionKeyCloakSettings.ClientId;
            string clientSecret = _SubmissionKeyCloakSettings.ClientSecret;
            string refreshToken = currentRefreshToken;

            HttpClientHandler handler = new HttpClientHandler();

            if (_SubmissionKeyCloakSettings.Proxy)
            {
                handler = new HttpClientHandler
                {
                    Proxy = new WebProxy(_SubmissionKeyCloakSettings.ProxyAddresURL, true), // Replace with your proxy server URL
                    UseProxy = _SubmissionKeyCloakSettings.Proxy,
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
                {"max_age", _SubmissionKeyCloakSettings.TokenRefreshSeconds} // Set a longer max_age in seconds
            });



            var tokenResponse = await httpClient.PostAsync(tokenEndpoint, tokenRequestBody);
            var tokenResponseContent = await tokenResponse.Content.ReadAsStringAsync();

            if (tokenResponse.IsSuccessStatusCode)
            {
                var tokenJson = JObject.Parse(tokenResponseContent);
                var newAccessToken = tokenJson["access_token"].ToString();
                result = newAccessToken;
                Log.Information("{Function} New Access Token with longer expiry: {newAccessToken}", "NewToken", newAccessToken);
            }
            else
            {
                Log.Error("{Function} Error refreshing token: {tokenResponseContent}", "NewToken", tokenResponseContent);
            }

            //var expirationDate = DateTime.Now.AddSeconds(int.Parse(_settings.TokenRefreshSeconds));
            return result;


        }
    }
}
