using Agent.Api.Models;
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
        public readonly IHealthCheckService _HealthCheckService;

        public HealthCheckController()
        {

        }

        [HttpPost("GetHealthCheckData")]
        public async Task<HealthCheckStatus> GetHealthCheckData()
        {
            var healthCheckData = await _HealthCheckService.GetHealthCheckData();
            return healthCheckData;
        }
    }
}
