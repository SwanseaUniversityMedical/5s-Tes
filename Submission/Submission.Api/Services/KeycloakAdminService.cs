using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using FiveSafesTes.Core.Models.Settings;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Serilog;
using Submission.Api.Services.Contract;

namespace Submission.Api.Services
{
    public class KeycloakAdminService : IKeycloakAdminService
    {
        private readonly SubmissionKeyCloakSettings _keycloakSettings;
        private readonly IConfiguration _configuration;

        public KeycloakAdminService(SubmissionKeyCloakSettings keycloakSettings, IConfiguration configuration)
        {
            _keycloakSettings = keycloakSettings;
            _configuration = configuration;
        }

        public async Task<(string clientUuid, string clientId, string clientSecret)> CreateServiceAccountAsync(string treName)
        {
            var clientId = GenerateClientId(treName);
            var adminToken = await GetAdminTokenAsync();

            var clientUuid = await CreateClientAsync(adminToken, clientId, treName);
            var clientSecret = await GetClientSecretAsync(adminToken, clientUuid);

            Log.Information("{Function} Created service account {ClientId} for TRE {TreName}",
                "CreateServiceAccountAsync", clientId, treName);

            return (clientUuid, clientId, clientSecret);
        }

        public async Task<bool> DeleteServiceAccountAsync(string clientUuid)
        {
            var adminToken = await GetAdminTokenAsync();
            var baseUrl = GetKeycloakBaseUrl();
            var realm = _keycloakSettings.Realm;

            using var client = new HttpClient(_keycloakSettings.getProxyHandler);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

            var url = $"{baseUrl}/admin/realms/{realm}/clients/{clientUuid}";
            var response = await client.DeleteAsync(url);

            if (response.IsSuccessStatusCode)
            {
                Log.Information("{Function} Deleted Keycloak client {ClientUuid}", "DeleteServiceAccountAsync", clientUuid);
                return true;
            }

            var content = await response.Content.ReadAsStringAsync();
            Log.Error("{Function} Failed to delete client {ClientUuid}. Status: {Status}, Response: {Response}",
                "DeleteServiceAccountAsync", clientUuid, response.StatusCode, content);
            return false;
        }

        private async Task<string> GetAdminTokenAsync()
        {
            var baseUrl = GetKeycloakBaseUrl();
            var adminUsername = _configuration["KeycloakAdmin:Username"];
            var adminPassword = _configuration["KeycloakAdmin:Password"];

            var url = $"{baseUrl}/realms/master/protocol/openid-connect/token";

            using var client = new HttpClient(_keycloakSettings.getProxyHandler);
            var tokenRequest = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                { "client_id", "admin-cli" },
                { "username", adminUsername },
                { "password", adminPassword },
                { "grant_type", "password" }
            });

            var response = await client.PostAsync(url, tokenRequest);
            var content = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                Log.Error("{Function} Failed to get admin token. Status: {Status}, Response: {Response}",
                    "GetAdminTokenAsync", response.StatusCode, content);
                throw new Exception($"Failed to get Keycloak admin token: {response.StatusCode}");
            }

            var tokenResponse = JObject.Parse(content);
            return tokenResponse["access_token"]?.ToString()
                ?? throw new Exception("Admin token response did not contain access_token");
        }

        private async Task<string> CreateClientAsync(string adminToken, string clientId, string treName)
        {
            var baseUrl = GetKeycloakBaseUrl();
            var realm = _keycloakSettings.Realm;

            using var client = new HttpClient(_keycloakSettings.getProxyHandler);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var clientPayload = new
            {
                clientId = clientId,
                name = $"TRE Agent - {treName}",
                enabled = true,
                protocol = "openid-connect",
                publicClient = false,
                serviceAccountsEnabled = true,
                clientAuthenticatorType = "client-secret",
                directAccessGrantsEnabled = false,
                standardFlowEnabled = false
            };

            var json = JsonConvert.SerializeObject(clientPayload);
            var url = $"{baseUrl}/admin/realms/{realm}/clients";

            var response = await client.PostAsync(url, new StringContent(json, Encoding.UTF8, "application/json"));

            if (response.StatusCode == System.Net.HttpStatusCode.Conflict)
            {
                Log.Warning("{Function} Client {ClientId} already exists, retrieving existing client",
                    "CreateClientAsync", clientId);
                return await GetClientUuidByClientIdAsync(adminToken, clientId);
            }

            if (!response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                Log.Error("{Function} Failed to create client {ClientId}. Status: {Status}, Response: {Response}",
                    "CreateClientAsync", clientId, response.StatusCode, content);
                throw new Exception($"Failed to create Keycloak client: {response.StatusCode} - {content}");
            }

            var locationHeader = response.Headers.Location?.ToString();
            if (!string.IsNullOrEmpty(locationHeader))
            {
                return locationHeader.Split('/').Last();
            }

            return await GetClientUuidByClientIdAsync(adminToken, clientId);
        }

        private async Task<string> GetClientUuidByClientIdAsync(string adminToken, string clientId)
        {
            var baseUrl = GetKeycloakBaseUrl();
            var realm = _keycloakSettings.Realm;

            using var client = new HttpClient(_keycloakSettings.getProxyHandler);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

            var url = $"{baseUrl}/admin/realms/{realm}/clients?clientId={clientId}";
            var response = await client.GetAsync(url);
            var content = await response.Content.ReadAsStringAsync();

            var clients = JArray.Parse(content);
            if (clients.Count == 0)
            {
                throw new Exception($"Client {clientId} not found in Keycloak");
            }

            return clients[0]["id"]?.ToString()
                ?? throw new Exception($"Client {clientId} found but has no UUID");
        }

        private async Task<string> GetClientSecretAsync(string adminToken, string clientUuid)
        {
            var baseUrl = GetKeycloakBaseUrl();
            var realm = _keycloakSettings.Realm;

            using var client = new HttpClient(_keycloakSettings.getProxyHandler);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

            var url = $"{baseUrl}/admin/realms/{realm}/clients/{clientUuid}/client-secret";
            var response = await client.GetAsync(url);
            var content = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                Log.Error("{Function} Failed to get client secret for {ClientUuid}. Status: {Status}",
                    "GetClientSecretAsync", clientUuid, response.StatusCode);
                throw new Exception($"Failed to get client secret: {response.StatusCode}");
            }

            var secretResponse = JObject.Parse(content);
            return secretResponse["value"]?.ToString()
                ?? throw new Exception("Client secret response did not contain value");
        }

        private string GetKeycloakBaseUrl()
        {
            var baseUrl = _keycloakSettings.BaseUrl;
            var realmSuffix = $"/realms/{_keycloakSettings.Realm}";
            if (baseUrl.EndsWith(realmSuffix))
            {
                baseUrl = baseUrl.Substring(0, baseUrl.Length - realmSuffix.Length);
            }
            return baseUrl;
        }

        private static string GenerateClientId(string treName)
        {
            var slug = Regex.Replace(treName.ToLower().Trim(), @"[^a-z0-9]+", "-").Trim('-');
            return $"tre-agent-{slug}";
        }
    }
}
