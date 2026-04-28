using System.Text.Json;
using Agent.Api.Models;
using FiveSafesTes.Core.Models.Settings;
using Microsoft.Extensions.Options;
using Serilog;
using VaultSharp;
using VaultSharp.V1.AuthMethods;
using VaultSharp.V1.AuthMethods.Token;
using VaultSharp.V1.Commons;

namespace Agent.Api.Services;

public class ConfigurationService : IConfigurationService
{
    private readonly VaultClient _vaultClient;

    public readonly VaultSettings _vaultSettings;
    // IOptionsMonitor used to retrieve latest values from vault at runtime.
    public readonly IOptionsMonitor<VaultConfigSettings> _configSettings;  
    public readonly IOptionsMonitor<SubmissionKeyCloakSettings> _keycloakSettings;  

    public ConfigurationService(IOptions<VaultSettings> vaultSettings, IOptionsMonitor<VaultConfigSettings> configSettings, IOptionsMonitor<SubmissionKeyCloakSettings> keycloakSettings)
    {
        _vaultSettings = vaultSettings?.Value ?? throw new ArgumentNullException(nameof(vaultSettings));
        _configSettings = configSettings;
        _keycloakSettings = keycloakSettings;

        IAuthMethodInfo authMethod = new TokenAuthMethodInfo(_vaultSettings.Token);
        VaultClientSettings vaultClientSettings = new(_vaultSettings.BaseUrl, authMethod);
        _vaultClient = new(vaultClientSettings);
    }

    /// <summary>
    /// Extract configuration values from a given json string and add or update the corresponding values in Vault.
    /// </summary>
    /// <param name="json">The json containing the configuration values we want to add to Vault.</param>
    /// <param name="prefix">The name of the model the given values should bind to when read by the vault configuration provider.</param>
    public async Task AddConfigurationToVault(string json, string prefix)
    {
        Dictionary<string, object>? config = DeserializeConfigJson(json);
        Dictionary<string, object> existingConfig;

        if (config != null)
        {
            try
            {
                Secret<SecretData> vaultData = await _vaultClient.V1.Secrets.KeyValue.V2.ReadSecretAsync(_vaultSettings.VaultConfigPath, mountPoint: _vaultSettings.SecretEngine);
                existingConfig = vaultData.Data.Data.ToDictionary();
            }
            catch (Exception)
            {
                // We don't have any secrets at this location yet.
                existingConfig = [];
            }

            // As vault is versioned, we must merge our existing data with our new data manually.
            Dictionary<string, object> mergedConfig = new(existingConfig);

            foreach (var kvp in config)
            {
                string key = $"{prefix}:{kvp.Key}";
                mergedConfig[key] = kvp.Value;
            }

            await _vaultClient.V1.Secrets.KeyValue.V2.WriteSecretAsync(_vaultSettings.VaultConfigPath, mergedConfig, mountPoint: _vaultSettings.SecretEngine);
            
        }
        else
        {
            Log.Error("ConfigurationService:AddConfigurationToVault - Invalid JSON format");
        }
    }

    /// <summary>
    /// Deserialize our json configuration into the vault configuration model. 
    /// </summary>
    /// <param name="json">The json containing our configuration values.</param>
    /// <returns>A model representing our vault configuration, or null if the input json is invalid.</returns>
    public Dictionary<string, object>? DeserializeConfigJson(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, object>>(json);
        }
        catch (Exception)
        {
            return null;
        }
    }
}
