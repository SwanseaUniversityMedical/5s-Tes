
using Agent.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Agent.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OnboardingController : Controller
{
    private readonly IOnboardingService _onboardingService;

    public OnboardingController(IOnboardingService onboardingService)
    {
        _onboardingService = onboardingService;
    }

    [HttpPost("UploadJsonConfig")]
    public async Task<IActionResult> UploadJsonConfig(IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest("No file uploaded.");
        }

        if (!Path.GetExtension(file.FileName).Equals(".json", StringComparison.CurrentCultureIgnoreCase))
        {
            return BadRequest("Configuration must be in JSON format.");
        }

        await _onboardingService.UploadJsonConfig(file);

        return Ok();
    }
}
