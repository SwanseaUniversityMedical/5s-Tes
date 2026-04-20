using System.Text.Json;
using Agent.Api.Models;
using FiveSafesTes.Core.Models.Settings;
using VaultSharp;
using VaultSharp.V1.AuthMethods;
using VaultSharp.V1.AuthMethods.Token;

namespace Agent.Api.Services;

public class ConfigurationService : IConfigurationService
{
    private readonly IVaultClient _vaultClient;
    public readonly VaultSettings _vaultSettings;

    public ConfigurationService(VaultSettings vaultSettings)
    {
        _vaultSettings = vaultSettings;

        IAuthMethodInfo authMethod = new TokenAuthMethodInfo(_vaultSettings.Token);
        VaultClientSettings vaultClientSettings = new(_vaultSettings.BaseUrl, authMethod);
        _vaultClient = new VaultClient(vaultClientSettings);
    }

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
            // invalid json format error
        }
    }

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
