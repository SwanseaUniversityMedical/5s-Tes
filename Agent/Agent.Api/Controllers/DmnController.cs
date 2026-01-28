using Agent.Api.Services;
using Credentials.Models.Models.Zeebe;
using Credentials.Models.Services;
using FiveSafesTes.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace Agent.Api.Controllers
{
    /// <summary>
    /// Controller for managing DMN (Decision Model and Notation) files
    /// </summary>
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "dare-tre-admin")]
    public class DmnController : ControllerBase
    {
        private readonly IDmnService _dmnService;
        private readonly IServicedZeebeClient _zeebeClient;
        private readonly ILogger<DmnController> _logger;
        private readonly DmnPath _DmnPath;
        private readonly string path;
        public DmnController(
            IDmnService dmnService,
            IServicedZeebeClient zeebeClient,
            ILogger<DmnController> logger,
            IConfiguration configuration,
            DmnPath DmnPath)
        {
            _dmnService = dmnService;
            _zeebeClient = zeebeClient;
            _logger = logger;

            _DmnPath = DmnPath;

            // Get DMN file path from configuration or use default
            var configuredPath = _DmnPath.Path;


            if (!string.IsNullOrEmpty(DmnPath.Path))
            {
                // Use configured path - make it absolute if relative
                if (Path.IsPathRooted(DmnPath.Path))
                {
                    path = Path.Combine(DmnPath.Path, "credentials.dmn");
                }
                else
                {
                    var projectDirectory = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", ".."));
                    path = Path.GetFullPath(Path.Combine(projectDirectory, DmnPath.Path, "credentials.dmn"));
                }
            }
            else
            {
                var projectDirectory = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", ".."));
                path = Path.GetFullPath(Path.Combine(projectDirectory, "..", "Tre-Camunda", "ProcessModels", "credentials.dmn"));
            }

            _logger.LogInformation($"DMN file path resolved to: {path}");
        }

        /// <summary>
        /// Get the complete DMN decision table
        /// </summary>
        /// <returns>DMN decision table with all rules</returns>
        [HttpGet("table")]
        [SwaggerOperation(Summary = "Get DMN decision table", Description = "Retrieves the complete DMN decision table including all inputs, outputs, and rules")]
        [ProducesResponseType(typeof(DmnDecisionTable), 200)]
        [ProducesResponseType(400)]
        public async Task<IActionResult> GetDmnTable()
        {
            try
            {
                var table = await _dmnService.LoadDmnTableAsync(path);
                return Ok(table);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading DMN table");
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Get all rules from the DMN table
        /// </summary>
        /// <returns>List of all DMN rules</returns>
        [HttpGet("rules")]
        [SwaggerOperation(Summary = "Get all DMN rules", Description = "Retrieves all rules from the DMN decision table")]
        [ProducesResponseType(typeof(List<DmnRule>), 200)]
        [ProducesResponseType(400)]
        public async Task<IActionResult> GetRules()
        {
            try
            {
                var table = await _dmnService.LoadDmnTableAsync(path);
                return Ok(table.Rules);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading DMN rules");
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Add a new rule to the DMN table
        /// </summary>
        /// <param name="request">Rule creation request with input and output values</param>
        /// <returns>The newly created rule</returns>
        [HttpPost("rules")]
        [SwaggerOperation(Summary = "Add new DMN rule", Description = "Creates a new rule in the DMN decision table")]
        [ProducesResponseType(typeof(DmnOperationResult), 200)]
        [ProducesResponseType(400)]
        public async Task<IActionResult> AddRule([FromBody] CreateDmnRuleRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var newRule = await _dmnService.AddRuleAsync(path, request);

                // Validate the updated DMN
                await _dmnService.ValidateDmnAsync(path);

                // Deploy to Zeebe
                await _dmnService.DeployDmnToZeebeAsync(path);

                return Ok(new DmnOperationResult
                {
                    Success = true,
                    Message = "Rule added successfully and deployed to Zeebe",
                    Data = newRule
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding DMN rule");
                return BadRequest(new DmnOperationResult
                {
                    Success = false,
                    Message = ex.Message
                });
            }
        }

        /// <summary>
        /// Update an existing rule in the DMN table
        /// </summary>
        /// <param name="request">Rule update request with rule ID and new values</param>
        /// <returns>Success status</returns>
        [HttpPut("rules")]
        [SwaggerOperation(Summary = "Update DMN rule", Description = "Updates an existing rule in the DMN decision table")]
        [ProducesResponseType(typeof(DmnOperationResult), 200)]
        [ProducesResponseType(400)]
        public async Task<IActionResult> UpdateRule([FromBody] UpdateDmnRuleRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                await _dmnService.UpdateRuleAsync(path, request);

                // Validate the updated DMN
                await _dmnService.ValidateDmnAsync(path);

                // Deploy to Zeebe
                await _dmnService.DeployDmnToZeebeAsync(path);

                return Ok(new DmnOperationResult
                {
                    Success = true,
                    Message = "Rule updated successfully and deployed to Zeebe"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating DMN rule");
                return BadRequest(new DmnOperationResult
                {
                    Success = false,
                    Message = ex.Message
                });
            }
        }

        /// <summary>
        /// Delete a rule from the DMN table
        /// </summary>
        /// <param name="ruleId">ID of the rule to delete</param>
        /// <returns>Success status</returns>
        [HttpDelete("rules/{ruleId}")]
        [SwaggerOperation(Summary = "Delete DMN rule", Description = "Deletes a rule from the DMN decision table")]
        [ProducesResponseType(typeof(DmnOperationResult), 200)]
        [ProducesResponseType(400)]
        public async Task<IActionResult> DeleteRule(string ruleId)
        {
            try
            {
                await _dmnService.DeleteRuleAsync(path, ruleId);

                // Validate the updated DMN
                await _dmnService.ValidateDmnAsync(path);

                // Deploy to Zeebe
                //await DeployDmnToZeebe();

                return Ok(new DmnOperationResult
                {
                    Success = true,
                    Message = "Rule deleted successfully and deployed to Zeebe"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting DMN rule");
                return BadRequest(new DmnOperationResult
                {
                    Success = false,
                    Message = ex.Message
                });
            }
        }

        /// <summary>
        /// Validate the DMN file structure
        /// </summary>
        /// <returns>Validation result</returns>
        [HttpGet("validate")]
        [SwaggerOperation(Summary = "Validate DMN", Description = "Validates the DMN file structure and rules")]
        [ProducesResponseType(typeof(DmnOperationResult), 200)]
        [ProducesResponseType(400)]
        public async Task<IActionResult> ValidateDmn()
        {
            try
            {
                await _dmnService.ValidateDmnAsync(path);
                return Ok(new DmnOperationResult
                {
                    Success = true,
                    Message = "DMN validation successful"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "DMN validation failed");
                return BadRequest(new DmnOperationResult
                {
                    Success = false,
                    Message = ex.Message
                });
            }
        }

        /// <summary>
        /// Test DMN evaluation with input variables
        /// </summary>
        /// <param name="request">Test request with input variables</param>
        /// <returns>Test result with matched rules</returns>
        [HttpPost("test")]
        [SwaggerOperation(Summary = "Test DMN evaluation", Description = "Tests the DMN with provided input variables and returns matching rules")]
        [ProducesResponseType(typeof(DmnTestResponse), 200)]
        [ProducesResponseType(400)]
        public async Task<IActionResult> TestDmn([FromBody] DmnTestRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                // Create DMN request for Zeebe
                var dmnRequest = new DmnRequest
                {
                    DecisionId = "CredentialsDMN",
                    Variables = request.InputVariables
                };

                // Evaluate using Zeebe
                var result = await _zeebeClient.EvaluateDecisionModelAsync(dmnRequest);

                return Ok(new DmnTestResponse
                {
                    Success = true,
                    Message = "DMN evaluation successful",
                    MatchedRules = new List<Dictionary<string, object>> { result.Result }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error testing DMN");
                return BadRequest(new DmnTestResponse
                {
                    Success = false,
                    Message = ex.Message
                });
            }
        }

        /// <summary>
        /// Deploy the DMN file to Zeebe
        /// </summary>
        /// <returns>Deployment result</returns>
        [HttpPost("deploy")]
        [SwaggerOperation(Summary = "Deploy DMN to Zeebe", Description = "Deploys the DMN file to the Zeebe workflow engine")]
        [ProducesResponseType(typeof(DmnOperationResult), 200)]
        [ProducesResponseType(400)]
        public async Task<IActionResult> DeployDmn()
        {
            try
            {
                await _dmnService.DeployDmnToZeebeAsync(path);
                return Ok(new DmnOperationResult
                {
                    Success = true,
                    Message = "DMN deployed successfully"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deploying DMN to Zeebe");
                return BadRequest(new DmnOperationResult
                {
                    Success = false,
                    Message = ex.Message
                });
            }
        }
    }
}
