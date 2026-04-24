using Agent.Api.Models;
using Agent.Api.Services;
using Google.Protobuf.WellKnownTypes;
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

    [HttpPost("AddConfigurationToVault/{json}")]
    public async Task AddConfigurationToVault(string json)
    {
        string decoded = Uri.UnescapeDataString(json); // TEMP - we'll use stringcontent when we're actually doing this
        await _configurationService.AddConfigurationToVault(decoded, nameof(VaultConfigSettings));
    }

    [HttpPost("ObserveConfig")]
    public async Task ObserveConfig()
    {
        _configurationService.ObserveConfig();
    }
}
