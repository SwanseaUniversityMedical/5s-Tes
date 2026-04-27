namespace Agent.Api.Services;

public interface IConfigurationService
{
    Dictionary<string, object>? DeserializeConfigJson(string json);
    Task AddConfigurationToVault(string json, string prefix);
    void ObserveConfig();
}
