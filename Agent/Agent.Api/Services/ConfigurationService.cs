using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
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
        _vaultClient = new VaultClient(vaultClientSettings);
    }

    /// <summary>
    /// Extract configuration values from a given json string and add or update the corresponding values in Vault.
    /// </summary>
    /// <param name="json">The json containing the configuration values we want to add to Vault.</param>
    public async Task AddConfigurationToVault(string json)
    {
        VaultConfigDTO? configDTO = DeserializeConfigJson(json);

        if (configDTO != null)
        {
            Dictionary<string, object?> configValues = new();

            // Add each value from the configuration settings
            foreach (KeyValuePair<string, object?> kvp in AppendPrefixToSettingsValues(configDTO.VaultConfigSettings, nameof(VaultConfigSettings)))
            {
                configValues[kvp.Key] = kvp.Value;
            }

            // Add each value from the submission keycloak settings
            foreach (KeyValuePair<string, object?> kvp in AppendPrefixToSettingsValues(configDTO.SubmissionKeyCloakSettings, nameof(SubmissionKeyCloakSettings)))
            {
                configValues[kvp.Key] = kvp.Value;
            }

            await _vaultClient.V1.Secrets.KeyValue.V2.WriteSecretAsync(
                path: _vaultSettings.VaultConfigPath,
                data: configValues,
                mountPoint: _vaultSettings.SecretEngine
            );
        }
        else
        {
            // invalid json format
        }
    }

    /// <summary>
    /// Deserialize our json configuration into the vault configuration model. 
    /// </summary>
    /// <param name="json">The json containing our configuration values.</param>
    /// <returns>A model representing our vault configuration, or null if the input json is invalid.</returns>
    public VaultConfigDTO? DeserializeConfigJson(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<VaultConfigDTO>(json);
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// Append the correct prefix to the start of each of our vault configuration values.
    /// </summary>
    /// <param name="settingsObj">The settings model containing the values we want to append the given prefix to.</param>
    /// <param name="prefix">The prefix we are appending to the values. Should match that of the appsettings.</param>
    /// <returns></returns>
    public static Dictionary<string, object?> AppendPrefixToSettingsValues(object settingsObj, string prefix)
    {
        Dictionary<string, object?> values = new();

        foreach (PropertyInfo property in settingsObj.GetType().GetProperties())
        {
            // Ignore values with the JsonIgnore attribute.
            if (Attribute.IsDefined(property, typeof(JsonIgnoreAttribute))) continue;

            object? value = property.GetValue(settingsObj);
            values[$"{prefix}:{property.Name}"] = value;
        }

        return values;
    }

    public void ObserveConfig()
    {
        var x = _keycloakSettings;
        var y = _configSettings;
    }
}
