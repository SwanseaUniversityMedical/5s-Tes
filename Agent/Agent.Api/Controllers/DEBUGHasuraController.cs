using Agent.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Serilog;

namespace Agent.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class DEBUGHasuraController : Controller
    {

        private readonly IHasuraService _iHasuraService;

        private readonly IDoSyncWork _iDoSyncWork;
        private readonly IDoAgentWork _iDoAgentWork;
        private readonly IKeyCloakService _IKeyCloakService;

        public DEBUGHasuraController(IHasuraService iHasuraService, IDoSyncWork iDoSyncWork,
           IDoAgentWork iDoAgentWork, IKeyCloakService IKeyCloakService)
        {
            _iHasuraService = iHasuraService;
            _iDoSyncWork = iDoSyncWork;
            _iDoAgentWork = iDoAgentWork;
            _IKeyCloakService = IKeyCloakService;
        }


        [HttpPost]
        [Route("RunHasuraSetup")]
        public async Task<IActionResult> RunHasuraSetup()
        {
            Log.Information("RunHasuraSetup");
            try
            {
                await _iHasuraService.Run();
                return StatusCode(200);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "{Function} Crash", "RunHasuraSetup");
                throw;
            }
        }
        [HttpPost]
        [Route("DoSyncWork")]
        public async Task<IActionResult> DoSyncWork()
        {
            Log.Information("DoSyncWork");
            try
            {
                _iDoSyncWork.Execute();
                return StatusCode(200);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "{Function} Crash", "DoSyncWork");
                throw;
            }
        }

        [HttpPost]
        [Route("DoAgentWork")]
        public async Task<IActionResult> DoAgentWork()
        {

            Log.Information("DoAgentWork");
            try
            {
                await _iDoAgentWork.Execute();
                return StatusCode(200);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "{Function} Crash", "DoAgentWork");
                throw;
            }
        }
    }
}
