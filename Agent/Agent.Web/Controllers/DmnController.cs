using FiveSafesTes.Core.Models;
using FiveSafesTes.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Agent.Web.Controllers
{
    /// <summary>
    /// Controller for DMN rule management UI
    /// </summary>
    [Authorize(Roles = "dare-tre-admin")]
    public class DmnController : Controller
    {
        private readonly ITREClientHelper _clientHelper;
        private readonly ILogger<DmnController> _logger;
        private readonly IConfiguration _configuration;

        public DmnController(
            ITREClientHelper clientHelper,
            ILogger<DmnController> logger,
            IConfiguration configuration)
        {
            _clientHelper = clientHelper;
            _logger = logger;
            _configuration = configuration;
        }

        /// <summary>
        /// Display the DMN rule management page
        /// </summary>
        /// <returns>View with DMN editor</returns>
        [HttpGet]
        public IActionResult Index()
        {
            try
            {
                _logger.LogInformation("DMN management page loaded for user: {User}", User?.Identity?.Name);
                return View();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading DMN management page");
                return View("Error");
            }
        }


        #region API Proxy Methods

        
        [HttpGet]
        [Route("Dmn/GetTable")]
        public async Task<IActionResult> GetTable()
        {
            try
            {
                var result = await _clientHelper.CallAPIWithoutModel<DmnDecisionTable>("/api/Dmn/table");
                return Json(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving DMN table");
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        
        [HttpGet]
        [Route("Dmn/GetRules")]
        public async Task<IActionResult> GetRules()
        {
            try
            {
                var result = await _clientHelper.CallAPIWithoutModel<DmnDecisionTable>("/api/Dmn/rules");
                return Json(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving DMN rules");
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        
        [HttpPost]
        [Route("Dmn/AddRule")]
        public async Task<IActionResult> AddRule([FromBody] CreateDmnRuleRequest request)
        {
            try
            {
                var result = await _clientHelper.CallAPI<CreateDmnRuleRequest, DmnOperationResult>("/api/Dmn/rules", request);
                return Json(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding DMN rule");
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        [HttpPut]
        [Route("Dmn/UpdateRule")]
        public async Task<IActionResult> UpdateRule([FromBody] UpdateDmnRuleRequest request)
        {
            try
            {
                var result = await _clientHelper.CallAPI<UpdateDmnRuleRequest, DmnOperationResult>("/api/Dmn/rules", request, usePut: true);
                return Json(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating DMN rule");
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        [HttpDelete]
        [Route("Dmn/DeleteRule/{ruleId}")]
        public async Task<IActionResult> DeleteRule(string ruleId)
        {
            try
            {
                var result = await _clientHelper.CallAPIDelete<DmnOperationResult>($"/api/Dmn/rules/{ruleId}");
                return Json(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting DMN rule");
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        [HttpGet]
        [Route("Dmn/ValidateDmn")]
        public async Task<IActionResult> ValidateDmn()
        {
            try
            {
                var result = await _clientHelper.CallAPIWithoutModel<DmnOperationResult>("/api/Dmn/validate");
                return Json(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating DMN");
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        [Route("Dmn/DeployDmn")]
        public async Task<IActionResult> DeployDmn()
        {
            try
            {
                var result = await _clientHelper.CallAPIWithoutModel<DmnOperationResult>("/api/Dmn/deploy", httpMethod: HttpMethod.Post);
                return Json(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deploying DMN to Zeebe");
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        #endregion
    }
}
