using Agent.Api.Models;
using Agent.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Agent.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ConfigurationController : Controller
{
    private readonly IConfigurationService _configurationService;

    public ConfigurationController(IConfigurationService configService)
    {
        _configurationService = configService;
    }

    [HttpPost("AddConfigurationToVault")]
    public async Task AddConfigurationToVault([FromBody] string json)
    {
        await _configurationService.AddConfigurationToVault(json, nameof(VaultConfigSettings));
    }
}
