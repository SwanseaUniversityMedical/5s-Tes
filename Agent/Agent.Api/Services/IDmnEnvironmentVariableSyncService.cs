namespace Agent.Api.Services;

public interface IDmnEnvironmentVariableSyncService
{
    Task SyncEnvironmentVariablesWithSubmission(string dmnPath);
}
