using FiveSafesTes.Core.Models;
using FiveSafesTes.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Agent.Web.Controllers
{
    [Authorize]
    [Route("[controller]")]
    public class HealthCheckController : Controller
    {
        private readonly ILogger<HealthCheckController> _logger;
        private readonly ITREClientHelper _treClientHelper;

        public HealthCheckController(ILogger<HomeController> logger, ITREClientHelper trehelper)
        {
          //_logger = logger;
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
