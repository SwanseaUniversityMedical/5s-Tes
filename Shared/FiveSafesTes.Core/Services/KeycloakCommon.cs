using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using IdentityModel.Client;
using Newtonsoft.Json;
using Serilog;

namespace FiveSafesTes.Core.Services
{
    public class KeycloakCommon
    {
        /// <summary>
        ///  use global env/config value for KeycloakDemoMode.
        /// </summary>
        public static bool ResolveKeycloakDemoMode(bool settingsValue, string? globalKeycloakDemoMode) =>
            settingsValue || string.Equals(globalKeycloakDemoMode, "true", StringComparison.OrdinalIgnoreCase);

        public static async Task<(string token, string Errorstring)> GetTokenForUserGuts(string username, string password, string requiredRole, HttpClientHandler proxyHandler,
            string keycloakBaseUrl, string clientId, string clientSecret, bool keycloakDemoMode, bool isServiceAccount)
        {

            Log.Information("{Function} keycloakBaseUrl > {BaseUrl} , _keycloakDemoMode: {_keycloakDemoMode}" , "GetTokenForUserGuts", keycloakBaseUrl, keycloakDemoMode);
           // Log.Information("{Function} username > " + username + " password: " + password, "GetTokenForUserGuts");
            var client = new HttpClient(proxyHandler);
            var disco = await client.GetDiscoveryDocumentAsync(new DiscoveryDocumentRequest
            {
                Address = keycloakBaseUrl,
                Policy = new DiscoveryPolicy
                {
                    RequireHttps = !keycloakDemoMode,
                    ValidateEndpoints = !keycloakDemoMode,
                    ValidateIssuerName = false, // Keycloak may have a different issuer name format
                }
            });

            if (disco.IsError)
            {
                Log.Error("{Function} {Error}", "GetTokenForUserGuts", disco.Error);
                return ("", disco.Error);
            }

            IdentityModel.Client.TokenResponse tokenResponse;

            if (!isServiceAccount)
            {
                tokenResponse = await client.RequestPasswordTokenAsync(new PasswordTokenRequest
                {
                    Address = disco.TokenEndpoint,
                    ClientId = clientId,
                    ClientSecret = clientSecret,
                    UserName = username,
                    Password = password
                });
            }
            else
            {
                tokenResponse = await client.RequestClientCredentialsTokenAsync(new ClientCredentialsTokenRequest
                {
                    Address = disco.TokenEndpoint,
                    ClientId = clientId,
                    ClientSecret = clientSecret
                });
            }

            if (tokenResponse.IsError)
            {
                Log.Error("{Function} {Error} for user {Username}", "GetTokenForUserGuts", tokenResponse.Error, username);
                return ("", tokenResponse.ErrorDescription);
            }


            var jwtHandler = new JwtSecurityTokenHandler();
            var token = jwtHandler.ReadJwtToken(tokenResponse.AccessToken);
            var groupClaims = token.Claims.Where(c => c.Type == "realm_access").Select(c => c.Value);
            var roles = new TokenRoles()
            {
                roles = new List<string>()
            };
            if (!string.IsNullOrWhiteSpace(requiredRole))
            {
                if (groupClaims.Any())
                {
                    roles = JsonConvert.DeserializeObject<TokenRoles>(groupClaims.First());
                }

                if (!roles.roles.Any(gc => gc.Equals(requiredRole)))
                {
                    var error = $"does not have correct role {requiredRole}";
                    Log.Information("{Function} User {Username} does not have correct role {AdminRole}",
                        "GetTokenForUserGuts", username, requiredRole);
                    return ("", error);
                }


                Log.Information("{Function} Token found with correct role {AdminRole} for User {Username}",
                    "GetTokenForUserGuts", requiredRole, username);
            }
            else
            {
                Log.Information("{Function} Token found for User {Username}, no role required",
                    "GetTokenForUserGuts", requiredRole, username);
            }
            return (tokenResponse.AccessToken, "");
        }
    }
}
