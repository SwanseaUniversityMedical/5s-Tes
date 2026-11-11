namespace Agent.Api.Services;

public interface IOnboardingService
{
    Task UploadJsonConfig(IFormFile file);
    Task<(bool Success, string Message)> UploadAirlockConfig(IFormFile file);
    bool IsAirlockConfigured();
    void RestartHangfireJobs();
    bool IsTRESynced();
    bool IsConfigurationUploaded();
    bool IsSyncJobCreated();
}
