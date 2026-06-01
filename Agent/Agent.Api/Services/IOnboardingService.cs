namespace Agent.Api.Services;

public interface IOnboardingService
{
    Task UploadJsonConfig(IFormFile file);
    void RestartHangfireJobs();
    bool IsTRESynced();
    bool IsConfigurationUploaded();
    void ClearVaultKeycloakDetails();
}
