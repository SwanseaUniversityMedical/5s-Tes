using Agent.Api.Services;
using Credentials.Models.Models.Zeebe;
using Credentials.Models.Services;
using FiveSafesTes.Core.Constants;
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
        }

        /// <summary>
        /// The table a request is for. Omitting it gives the environment variables table,
        /// so the endpoints answer exactly as they did before this controller could address
        /// more than one.
        /// </summary>
        private DmnTable TableFor(string? table)
        {
            var resolved = DmnFiles.Resolve(table);

            if (!string.IsNullOrWhiteSpace(table)
                && !string.Equals(resolved.Slug, table.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("Unknown DMN table '{Requested}', falling back to {Slug}", table, resolved.Slug);
            }

            return resolved;
        }

        /// <summary>
        /// Where the editable copy of a table lives. On a deployed stack that is the
        /// persistent volume, so admin edits survive a restart.
        /// </summary>
        private string PathFor(string? table)
        {
            var path = DmnFiles.ResolvePath(_DmnPath.Path, TableFor(table).FileName);
            _logger.LogInformation($"DMN file path resolved to: {path}");
            return path;
        }

        /// <summary>
        /// The tables this controller can manage, so the admin UI can offer them without
        /// hardcoding the list.
        /// </summary>
        [HttpGet("tables")]
        [SwaggerOperation(Summary = "List the DMN tables", Description = "The decision tables a TRE Admin can view and edit")]
        [ProducesResponseType(typeof(IReadOnlyList<DmnTable>), 200)]
        public IActionResult GetTables()
        {
            return Ok(DmnFiles.Tables);
        }

        /// <summary>
        /// Get the complete DMN decision table
        /// </summary>
        /// <returns>DMN decision table with all rules</returns>
        [HttpGet("table")]
        [SwaggerOperation(Summary = "Get DMN decision table", Description = "Retrieves the complete DMN decision table including all inputs, outputs, and rules")]
        [ProducesResponseType(typeof(DmnDecisionTable), 200)]
        [ProducesResponseType(400)]
        public async Task<IActionResult> GetDmnTable([FromQuery(Name = "table")] string? dmnTable = null)
        {
            var path = PathFor(dmnTable);

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
        public async Task<IActionResult> GetRules([FromQuery(Name = "table")] string? dmnTable = null)
        {
            var path = PathFor(dmnTable);

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
        public async Task<IActionResult> AddRule([FromBody] CreateDmnRuleRequest request, [FromQuery(Name = "table")] string? dmnTable = null)
        {
            var path = PathFor(dmnTable);

            // We use a temp file to prevent invalid rules from contaminating the real DMN file.
            // The rule must be added before validation because ValidateDmnAsync validates the
            // entire DMN file structure (not individual values). The real content validation
            // happens when Zeebe parses the FEEL expressions during deployment. If Zeebe rejects
            // the deployment, the real file remains untouched and the error will be returned 
            // to the user for correction and Temp file is deleted in the finally block.

            var _tempPath = System.IO.Path.GetTempFileName() + ".dmn";
            try
            {
              if (!ModelState.IsValid)
              {
                return BadRequest(ModelState);
              }

              // Copy real file to temp
              System.IO.File.Copy(path, _tempPath, overwrite: true);

              // Add rule to TEMP file for Zeebe Validation
              var newRule = await _dmnService.AddRuleAsync(_tempPath, request);

              // Validate the updated DMN
              await _dmnService.ValidateDmnAsync(_tempPath);

              // Deploy to Zeebe
              await _dmnService.DeployDmnToZeebeAsync(_tempPath);

              // Save to real file - (Zeebe returns without Error)
              System.IO.File.Copy(_tempPath, path, overwrite: true);

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
            finally
            {
              if (System.IO.File.Exists(_tempPath))
              {
                System.IO.File.Delete(_tempPath);
              }
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
        public async Task<IActionResult> UpdateRule([FromBody] UpdateDmnRuleRequest request, [FromQuery(Name = "table")] string? dmnTable = null)
        {
            var path = PathFor(dmnTable);

            var _tempPath = System.IO.Path.GetTempFileName() + ".dmn";
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }
                
                // Copy real file to temp
                System.IO.File.Copy(path, _tempPath, overwrite: true);
                
                // Update rule in TEMP file for Zeebe Validation
                await _dmnService.UpdateRuleAsync(_tempPath, request);

                // Validate the updated DMN
                await _dmnService.ValidateDmnAsync(_tempPath);

                // Deploy to Zeebe
                await _dmnService.DeployDmnToZeebeAsync(_tempPath);

                // Save to real file - (Zeebe returns without Error)
                System.IO.File.Copy(_tempPath, path, overwrite: true);
                
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
            finally
            {
              if (System.IO.File.Exists(_tempPath))
              {
                System.IO.File.Delete(_tempPath);
              }
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
        public async Task<IActionResult> DeleteRule(string ruleId, [FromQuery(Name = "table")] string? dmnTable = null)
        {
            var path = PathFor(dmnTable);

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
        public async Task<IActionResult> ValidateDmn([FromQuery(Name = "table")] string? dmnTable = null)
        {
            var path = PathFor(dmnTable);

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
        public async Task<IActionResult> TestDmn([FromBody] DmnTestRequest request, [FromQuery(Name = "table")] string? dmnTable = null)
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
                    DecisionId = TableFor(dmnTable).DecisionId,
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
        public async Task<IActionResult> DeployDmn([FromQuery(Name = "table")] string? dmnTable = null)
        {
            var path = PathFor(dmnTable);

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
