namespace Agent.Api.Services;

public interface IOnboardingService
{
    Task UploadJsonConfig(IFormFile file);
}
