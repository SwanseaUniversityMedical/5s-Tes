using Agent.Api.Models;

namespace Agent.Api.Services;

public interface IConfigurationService
{
    Dictionary<string, object>? DeserializeConfigJsonToDictionary(string json);
    VaultConfigSettings? DeserializeConfigJson(string json);
    Task AddConfigurationToVault(string json);
}
