using System.Text.Json;
using Agent.Api.Models;
using FiveSafesTes.Core.Models.Settings;
using Microsoft.Extensions.Options;
using VaultSharp;
using VaultSharp.V1.AuthMethods;
using VaultSharp.V1.AuthMethods.Token;

namespace Agent.Api.Services;

public class ConfigurationService : IConfigurationService
{
    private readonly IVaultClient _vaultClient;
    public readonly VaultSettings _vaultSettings;
    // IOptionsMonitor allows us to retrieve the most recent configuration values at runtime.
    public readonly IOptionsMonitor<VaultConfigSettings> _configSettings;  

    public ConfigurationService(VaultSettings vaultSettings, IOptionsMonitor<VaultConfigSettings> configSettings)
    {
        _vaultSettings = vaultSettings;
        _configSettings = configSettings;

        IAuthMethodInfo authMethod = new TokenAuthMethodInfo(_vaultSettings.Token);
        VaultClientSettings vaultClientSettings = new(_vaultSettings.BaseUrl, authMethod);
        _vaultClient = new VaultClient(vaultClientSettings);
    }

    /// <summary>
    /// Extract configuration values from a given json string and add or update the corresponding values in Vault.
    /// </summary>
    /// <param name="json">The json containing the configuration values we want to add to Vault.</param>
    public async Task AddConfigurationToVault(string json)
    {
        Dictionary<string, object>? config = DeserializeConfigJsonToDictionary(json);

        if (config != null)
        {
            await _vaultClient.V1.Secrets.KeyValue.V2.WriteSecretAsync(
                path: _vaultSettings.VaultConfigPath,
                data: config,
                mountPoint: _vaultSettings.SecretEngine
            );
        }
        else
        {
            // invalid json format
        }
    }

    /// <summary>
    /// Deserialize our json configuration into a dictionary. 
    /// </summary>
    /// <param name="json">The json containing our configuration values.</param>
    /// <returns>A dictionary containing the configuration, or null if the input json is invalid.</returns>
    public Dictionary<string, object>? DeserializeConfigJsonToDictionary(string json)
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

    /// <summary>
    /// Deserialize our json configuration into the vault configuration model. 
    /// </summary>
    /// <param name="json">The json containing our configuration values.</param>
    /// <returns>A model representing our vault configuration, or null if the input json is invalid.</returns>
    public VaultConfigSettings? DeserializeConfigJson(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<VaultConfigSettings>(json);
        }
        catch (Exception)
        {
            return null;
        }
    }
}
