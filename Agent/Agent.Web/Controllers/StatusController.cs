using FiveSafesTes.Core.Models;
using FiveSafesTes.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Agent.Web.Controllers
{
    [Authorize]
    [Route("[controller]")]
    public class StatusController : Controller
    {
        private readonly ILogger<StatusController> _logger;
        private readonly ITREClientHelper _treClientHelper;

        public StatusController(ILogger<StatusController> logger, ITREClientHelper trehelper)
        {
            _logger = logger;
            _treClientHelper = trehelper;
        }

        [Route("GetHealthCheckData")]
        public async Task<IActionResult> GetHealthCheckData()
        {
            var data = _treClientHelper.CallAPIWithoutModel<List<HealthCheckStatus>>("api/HealthCheck/GetHealthCheckData").Result;

            return View(data);
        }
    }
}
