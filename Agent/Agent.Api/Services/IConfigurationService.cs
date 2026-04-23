using Agent.Api.Models;

namespace Agent.Api.Services;

public interface IConfigurationService
{
    VaultConfigDTO? DeserializeConfigJson(string json);
    Task AddConfigurationToVault(string json);
    void ObserveConfig();
}
