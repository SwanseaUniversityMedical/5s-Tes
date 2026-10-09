using System.Text.Json;
using FiveSafesTes.Core.Constants;
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

        /// <summary>
        /// The part of a failed API call worth showing an admin.
        ///
        /// BaseClientHelper puts the whole API response inside its exception message, so the
        /// useful text - a FEEL parse error, say - ends up wrapped in two layers of JSON.
        /// Rules are written by hand, so these errors are read often and need to be legible.
        /// </summary>
        private static string Readable(Exception exception)
        {
            var message = exception.Message;
            var start = message.IndexOf('{');

            if (start < 0)
            {
                return message;
            }

            try
            {
                using var document = JsonDocument.Parse(message[start..]);

                if (document.RootElement.TryGetProperty("message", out var inner))
                {
                    var text = inner.GetString();
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        return text;
                    }
                }
            }
            catch (JsonException)
            {
                // Not the shape we expected; the raw message is better than nothing.
            }

            return message;
        }

        /// <summary>
        /// Carries the chosen table through to the API as a query parameter. Null means the
        /// API picks its default, which is the environment variables table.
        /// </summary>
        private static Dictionary<string, string>? TableParams(string? table)
        {
            return string.IsNullOrWhiteSpace(table)
                ? null
                : new Dictionary<string, string> { ["table"] = table };
        }

        /// <summary>
        /// The DMN tables a TRE Admin can manage, so the page does not hardcode the list.
        /// </summary>
        [HttpGet]
        [Route("Dmn/GetTables")]
        public async Task<IActionResult> GetTables()
        {
            try
            {
                var result = await _clientHelper.CallAPIWithoutModel<List<DmnTable>>("/api/Dmn/tables");
                return Json(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving the list of DMN tables");
                return Json(new { success = false, message = Readable(ex) });
            }
        }


        
        [HttpGet]
        [Route("Dmn/GetTable")]
        public async Task<IActionResult> GetTable(string? table = null)
        {
            try
            {
                var result = await _clientHelper.CallAPIWithoutModel<DmnDecisionTable>("/api/Dmn/table", TableParams(table));
                return Json(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving DMN table");
                return BadRequest(new { success = false, message = Readable(ex) });
            }
        }

        
        [HttpGet]
        [Route("Dmn/GetRules")]
        public async Task<IActionResult> GetRules(string? table = null)
        {
            try
            {
                var result = await _clientHelper.CallAPIWithoutModel<DmnDecisionTable>("/api/Dmn/rules", TableParams(table));
                return Json(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving DMN rules");
                return BadRequest(new { success = false, message = Readable(ex) });
            }
        }

        
        [HttpPost]
        [Route("Dmn/AddRule")]
        public async Task<IActionResult> AddRule([FromBody] CreateDmnRuleRequest request, string? table = null)
        {
            try
            {
                var result = await _clientHelper.CallAPI<CreateDmnRuleRequest, DmnOperationResult>("/api/Dmn/rules", request, TableParams(table));
                return Json(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding DMN rule");
                return BadRequest(new { success = false, message = Readable(ex) });
            }
        }

        [HttpPut]
        [Route("Dmn/UpdateRule")]
        public async Task<IActionResult> UpdateRule([FromBody] UpdateDmnRuleRequest request, string? table = null)
        {
            try
            {
                var result = await _clientHelper.CallAPI<UpdateDmnRuleRequest, DmnOperationResult>("/api/Dmn/rules", request, TableParams(table), usePut: true);
                return Json(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating DMN rule");
                return BadRequest(new { success = false, message = Readable(ex) });
            }
        }

        [HttpDelete]
        [Route("Dmn/DeleteRule/{ruleId}")]
        public async Task<IActionResult> DeleteRule(string ruleId, string? table = null)
        {
            try
            {
                var result = await _clientHelper.CallAPIDelete<DmnOperationResult>($"/api/Dmn/rules/{ruleId}", TableParams(table));
                return Json(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting DMN rule");
                return BadRequest(new { success = false, message = Readable(ex) });
            }
        }

        [HttpGet]
        [Route("Dmn/ValidateDmn")]
        public async Task<IActionResult> ValidateDmn(string? table = null)
        {
            try
            {
                var result = await _clientHelper.CallAPIWithoutModel<DmnOperationResult>("/api/Dmn/validate", TableParams(table));
                return Json(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating DMN");
                return BadRequest(new { success = false, message = Readable(ex) });
            }
        }

        [HttpPost]
        [Route("Dmn/DeployDmn")]
        public async Task<IActionResult> DeployDmn(string? table = null)
        {
            try
            {
                var result = await _clientHelper.CallAPIWithoutModel<DmnOperationResult>("/api/Dmn/deploy", TableParams(table), httpMethod: HttpMethod.Post);
                return Json(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deploying DMN to Zeebe");
                return BadRequest(new { success = false, message = Readable(ex) });
            }
        }

        #endregion
    }
}
