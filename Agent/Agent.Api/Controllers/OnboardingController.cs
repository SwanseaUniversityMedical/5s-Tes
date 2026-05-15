
using Agent.Api.Services;
using FiveSafesTes.Core.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Agent.Api.Controllers;

[ApiController]
[Authorize(Roles = "dare-tre-admin")]
[Route("api/[controller]")]
public class OnboardingController : Controller
{
    private readonly IOnboardingService _onboardingService;

    public OnboardingController(IOnboardingService onboardingService)
    {
        _onboardingService = onboardingService;
    }

    [HttpPost("UploadJsonConfig")]
    public async Task<JsonConfigUploadResponse> UploadJsonConfig(IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            return new() 
            { 
                Success = false,
                Message = "No file uploaded!"
            };
        }

        if (!Path.GetExtension(file.FileName).Equals(".json", StringComparison.CurrentCultureIgnoreCase))
        {
            return new()
            {
                Success = false,
                Message = "Configuration must be in JSON Format!"
            };
        }

        await _onboardingService.UploadJsonConfig(file);

        return new()
        {
            Success = true,
            Message = "File uploaded successfully."
        };
    }
}
