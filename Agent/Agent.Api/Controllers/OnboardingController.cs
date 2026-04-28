
using Agent.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Agent.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OnboardingController
{
    private readonly IOnboardingService _onboardingService;

    public OnboardingController(IOnboardingService onboardingService)
    {
        _onboardingService = onboardingService;
    }

    [HttpPost("AddKeycloakSettingsToVault")]
    public async Task AddKeycloakSettingsToVault()
    {
        await _onboardingService.AddKeycloakSettingsToVault();
    }
}
