using FiveSafesTes.Core.Models;
using Agent.Api.Repositories.DbContexts;
using Agent.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Agent.Api.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class HealthCheckController : Controller
    {
        public readonly ApplicationDbContext _DbContext;
        public readonly IHealthCheckService _healthCheckService;

        public HealthCheckController(IHealthCheckService healthCheckService)
        {
            _healthCheckService = healthCheckService;
        }

        [HttpGet("GetHealthCheckData")]
        public List<HealthCheckStatus> GetHealthCheckData()
        {
            var healthCheckData = _healthCheckService.GetHealthCheckData();
            return healthCheckData;
        }
    }
}
