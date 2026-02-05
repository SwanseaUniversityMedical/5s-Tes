using Newtonsoft.Json;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Credentials.Camunda.Settings;
using Microsoft.Extensions.Options;


namespace Credentials.Camunda.Services
{
    public class VaultCredentialsService : IVaultCredentialsService, IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly VaultSettings _vaultSettings;
        private bool _disposed = false;

        public VaultCredentialsService(HttpClient httpClient, IOptions<VaultSettings> options)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _vaultSettings = options?.Value ?? throw new ArgumentNullException(nameof(options));

        }      

        public async Task<bool> AddCredentialAsync(string path, Dictionary<string, object> credential)
        {
            return await ExecuteWithRetryAsync(async () =>
            {
                Log.Information("Adding credential to vault path: {Path}", path);

                var payload = new VaultSecretPayload { Data = credential };
                var jsonContent = JsonConvert.SerializeObject(payload);
                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync($"v1/{_vaultSettings.SecretEngine}/data/{path}", content);

                if (response.IsSuccessStatusCode)
                {
                    Log.Information("Successfully added credential to vault path: {Path}", path);
                    return true;
                }

                var errorContent = await response.Content.ReadAsStringAsync();
                Log.Error("Failed to add credential to vault. Status: {Status}, Error: {Error}",
                    response.StatusCode, errorContent);

                return false;
            });
        }

        public async Task<bool> RemoveCredentialAsync(string path)
        {
            return await ExecuteWithRetryAsync(async () =>
            {
                Log.Information("Removing credential from vault path: {Path}", path);

                var response = await _httpClient.DeleteAsync($"v1/{_vaultSettings.SecretEngine}/data/{path}");

                if (response.IsSuccessStatusCode)
                {
                    Log.Information("Successfully removed credential from vault path: {Path}", path);
                    return true;
                }

                var errorContent = await response.Content.ReadAsStringAsync();
                Log.Error("Failed to remove credential from vault. Status: {Status}, Error: {Error}",
                    response.StatusCode, errorContent);

                return false;
            });
        }

        public async Task<Dictionary<string, object>> GetCredentialAsync(string path)
        {
            return await ExecuteWithRetryAsync(async () =>
            {
                Log.Information("Retrieving credential from vault path: {Path}", path);

                var response = await _httpClient.GetAsync($"v1/{_vaultSettings.SecretEngine}/data/{path}");

                if (response.IsSuccessStatusCode)
                {
                    var jsonContent = await response.Content.ReadAsStringAsync();
                    var vaultResponse = JsonConvert.DeserializeObject<VaultSecretResponse>(jsonContent);

                    Log.Information("Successfully retrieved credential from vault path: {Path}", path);
                    return vaultResponse?.Data?.Data ?? new Dictionary<string, object>();
                }

                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    Log.Warning("Credential not found at vault path: {Path}", path);
                    return new Dictionary<string, object>();
                }

                var errorContent = await response.Content.ReadAsStringAsync();
                Log.Error("Failed to retrieve credential from vault. Status: {Status}, Error: {Error}",
                    response.StatusCode, errorContent);

                return new Dictionary<string, object>();
            });
        }

        public async Task<bool> UpdateCredentialAsync(string path, Dictionary<string, object> credential)
        {
            return await ExecuteWithRetryAsync(async () =>
            {
                Log.Information("Updating credential at vault path: {Path}", path);

                var payload = new VaultSecretPayload { Data = credential };
                var jsonContent = JsonConvert.SerializeObject(payload);
                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                var response = await _httpClient.PutAsync($"v1/{_vaultSettings.SecretEngine}/data/{path}", content);

                if (response.IsSuccessStatusCode)
                {
                    Log.Information("Successfully updated credential at vault path: {Path}", path);
                    return true;
                }

                var errorContent = await response.Content.ReadAsStringAsync();
                Log.Error("Failed to update credential in vault. Status: {Status}, Error: {Error}",
                    response.StatusCode, errorContent);

                return false;
            });
        }

        public async Task<string> GetConnectionStringAsync(string databaseName)
        {
            try
            {
                var credentials = await GetCredentialAsync($"database/{databaseName}");

                if (credentials.Count == 0)
                {
                    Log.Warning("No database credentials found for: {DatabaseName}", databaseName);
                    return string.Empty;
                }

                var server = credentials.GetValueOrDefault("server", "localhost").ToString();
                var port = credentials.GetValueOrDefault("port", "5432").ToString();
                var database = credentials.GetValueOrDefault("database", databaseName).ToString();
                var username = credentials.GetValueOrDefault("username", "").ToString();
                var password = credentials.GetValueOrDefault("password", "").ToString();

                var connectionString = $"Server={server};Port={port};Database={database};User Id={username};Password={password};Include Error Detail=true;";

                Log.Information("Successfully built connection string for database: {DatabaseName}", databaseName);
                return connectionString;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to get connection string for database: {DatabaseName}", databaseName);
                return string.Empty;
            }
        }

        public async Task<bool> StoreConnectionStringAsync(string databaseName, string server, string database, string username, string password, int port = 5432)
        {
            var credentials = new Dictionary<string, object>
            {
                ["server"] = server,
                ["port"] = port,
                ["database"] = database,
                ["username"] = username,
                ["password"] = password,
                ["created_at"] = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                ["type"] = "postgresql"
            };

            return await AddCredentialAsync($"database/{databaseName}", credentials);
        }

        private async Task<T> ExecuteWithRetryAsync<T>(Func<Task<T>> operation)
        {
            if (!_vaultSettings.EnableRetry)
            {
                return await operation();
            }

            int attempts = 0;
            Exception lastException = null;

            while (attempts < _vaultSettings.MaxRetryAttempts)
            {
                try
                {
                    return await operation();
                }
                catch (HttpRequestException ex)
                {
                    lastException = ex;
                    attempts++;

                    if (attempts < _vaultSettings.MaxRetryAttempts)
                    {
                        var delay = TimeSpan.FromSeconds(Math.Pow(2, attempts)); // Exponential backoff
                        Log.Warning("Vault operation failed, retrying in {Delay}ms. Attempt {Attempt}/{MaxAttempts}. Error: {Error}",
                            delay.TotalMilliseconds, attempts, _vaultSettings.MaxRetryAttempts, ex.Message);

                        await Task.Delay(delay);
                    }
                }
            }

            Log.Error(lastException, "Vault operation failed after {MaxAttempts} attempts", _vaultSettings.MaxRetryAttempts);
            return default(T);
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _httpClient?.Dispose();
                _disposed = true;
            }
        }
    }

    // Supporting classes
    public class VaultSecretPayload
    {
        [JsonProperty("data")]
        public Dictionary<string, object> Data { get; set; } = new();
    }

    public class VaultSecretResponse
    {
        [JsonProperty("data")]
        public VaultSecretData Data { get; set; } = new();
    }

    public class VaultSecretData
    {
        [JsonProperty("data")]
        public Dictionary<string, object> Data { get; set; } = new();

        [JsonProperty("metadata")]
        public VaultMetadata Metadata { get; set; } = new();
    }

    public class VaultMetadata
    {
        [JsonProperty("created_time")]
        public DateTime? CreatedTime { get; set; }

        [JsonProperty("deletion_time")]
        public DateTime? DeletionTime { get; set; }

        [JsonProperty("destroyed")]
        public bool Destroyed { get; set; }

        [JsonProperty("version")]
        public int Version { get; set; }
    }
}
