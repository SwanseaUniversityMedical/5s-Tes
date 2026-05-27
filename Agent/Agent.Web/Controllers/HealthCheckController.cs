using Agent.Api.Models;
using FiveSafesTes.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Agent.Web.Controllers
{
    [Authorize]
    [Route("[controller]")]
    public class HealthCheckController
    {
        private readonly ILogger<HealthCheckController> _logger;
        private readonly ITREClientHelper _treClientHelper;

        public HealthCheckController(ILogger<HomeController> logger, ITREClientHelper trehelper)
        {
          _logger = logger;
          _treClientHelper = trehelper;
        }

        [Route("GetHealthCheckData")]
        public async Task<IActionResult> GetHealthCheckData()
        {
            var healthCheckData = await _treClientHelper.CallAPIWithoutModel<HealthCheckStatus>("api/HealthCheck/GetHealthCheckData");
            
        }
    }

}
