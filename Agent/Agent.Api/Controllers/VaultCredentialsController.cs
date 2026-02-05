//using FiveSafesTes.Core.Services.Contract;
//using Microsoft.AspNetCore.Http;
//using Microsoft.AspNetCore.Mvc;
//using Serilog;
//using Swashbuckle.AspNetCore.Annotations;
//using System.ComponentModel.DataAnnotations;

//namespace Agent.Api.Controllers
//{
//    [Route("api/v1/vaultcredentials")]
//    [ApiController]
//    [SwaggerTag("Vaultcredentials", "Manage Credentials")]
//    [Produces("application/json")]
//    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
//    public class VaultCredentialsController : ControllerBase
//    {
//        private readonly IVaultCredentialsService _vaultService;
//        public VaultCredentialsController(IVaultCredentialsService vaultService)
//        {
//            _vaultService = vaultService ?? throw new ArgumentNullException(nameof(vaultService));
//        }

//        /// <summary>
//        /// Creates ephemeral credentials for a user's container execution
//        /// </summary>
//        /// <param name="request">The ephemeral credential request</param>
//        /// <returns>Success status and credential path</returns>
//        [HttpPost("ephemeral-credentials")]
//        [ProducesResponseType(typeof(EphemeralCredentialResponse), 200)]
//        [ProducesResponseType(400)]
//        [ProducesResponseType(500)]
//        public async Task<ActionResult<EphemeralCredentialResponse>> CreateEphemeralCredentials(
//            [FromBody] CreateEphemeralCredentialRequest request)
//        {
//            try
//            {
//                if (!ModelState.IsValid)
//                {
//                    return BadRequest(ModelState);
//                }

//                var credentialPath = $"ephemeral/{request.UserId}/{request.JobId}";

//                var credentials = new Dictionary<string, object>();

//                switch (request.ServiceType.ToLower())
//                {
//                    case "postgres":
//                    case "trino":
//                        credentials = new Dictionary<string, object>
//                        {
//                            ["username"] = $"temp_user_{request.JobId}_{DateTime.UtcNow:yyyyMMddHHmmss}",
//                            ["password"] = GenerateSecurePassword(),
//                            ["server"] = request.ServerAddress,
//                            ["port"] = request.Port,
//                            ["database"] = request.DatabaseName,
//                            ["service_type"] = request.ServiceType,
//                            ["user_id"] = request.UserId,
//                            ["job_id"] = request.JobId,
//                            ["created_at"] = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
//                            ["expires_at"] = DateTime.UtcNow.AddHours(request.ExpirationHours).ToString("yyyy-MM-ddTHH:mm:ssZ"),
//                            ["description"] = request.Description
//                        };
//                        break;

//                    case "minio":
//                        credentials = new Dictionary<string, object>
//                        {
//                            ["access_key"] = $"temp_access_{request.JobId}",
//                            ["secret_key"] = GenerateSecurePassword(32),
//                            ["endpoint"] = request.ServerAddress,
//                            ["bucket"] = request.BucketName,
//                            ["service_type"] = request.ServiceType,
//                            ["user_id"] = request.UserId,
//                            ["job_id"] = request.JobId,
//                            ["created_at"] = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
//                            ["expires_at"] = DateTime.UtcNow.AddHours(request.ExpirationHours).ToString("yyyy-MM-ddTHH:mm:ssZ"),
//                            ["description"] = request.Description
//                        };
//                        break;

//                    default:
//                        return BadRequest($"Unsupported service type: {request.ServiceType}");
//                }

//                var result = await _vaultService.AddCredentialAsync(credentialPath, credentials);

//                if (result)
//                {
//                    Log.Information("Created ephemeral credentials for user {UserId}, job {JobId}, service {ServiceType}",
//                        request.UserId, request.JobId, request.ServiceType);

//                    return Ok(new EphemeralCredentialResponse
//                    {
//                        Success = true,
//                        CredentialPath = credentialPath,
//                        ExpiresAt = DateTime.UtcNow.AddHours(request.ExpirationHours),
//                        Message = "Ephemeral credentials created successfully"
//                    });
//                }

//                return StatusCode(500, new { Message = "Failed to create credentials in Vault" });
//            }
//            catch (Exception ex)
//            {
//                Log.Error(ex, "Error creating ephemeral credentials for user {UserId}, job {JobId}",
//                    request.UserId, request.JobId);
//                return StatusCode(500, new { Message = "Internal server error", Error = ex.Message });
//            }
//        }

//        // <summary>
//        /// Retrieves ephemeral credentials for a job
//        /// </summary>
//        /// <param name="userId">User identifier</param>
//        /// <param name="jobId">Job identifier</param>
//        [HttpGet("ephemeral-credentials/{userId}/{jobId}")]
//        [ProducesResponseType(typeof(Dictionary<string, object>), 200)]
//        [ProducesResponseType(404)]
//        [ProducesResponseType(500)]
//        public async Task<ActionResult<Dictionary<string, object>>> GetEphemeralCredentials(
//            [FromRoute] string userId,
//            [FromRoute] string jobId)
//        {
//            try
//            {
//                var credentialPath = $"ephemeral/{userId}/{jobId}";
//                var credentials = await _vaultService.GetCredentialAsync(credentialPath);

//                if (credentials.Count == 0)
//                {
//                    return NotFound(new { Message = "Credentials not found" });
//                }

//                // Remove sensitive internal fields from response
//                var safeCredentials = new Dictionary<string, object>(credentials);
//                if (safeCredentials.ContainsKey("password"))
//                {
//                    safeCredentials["password"] = "***REDACTED***";
//                }
//                if (safeCredentials.ContainsKey("secret_key"))
//                {
//                    safeCredentials["secret_key"] = "***REDACTED***";
//                }

//                return Ok(safeCredentials);
//            }
//            catch (Exception ex)
//            {
//                Log.Error(ex, "Error retrieving ephemeral credentials for user {UserId}, job {JobId}", userId, jobId);
//                return StatusCode(500, new { Message = "Internal server error", Error = ex.Message });
//            }
//        }

//        /// <summary>
//        /// Removes ephemeral credentials after job completion
//        /// </summary>
//        /// <param name="userId">User identifier</param>
//        /// <param name="jobId">Job identifier</param>
//        [HttpDelete("ephemeral-credentials/{userId}/{jobId}")]
//        [ProducesResponseType(200)]
//        [ProducesResponseType(404)]
//        [ProducesResponseType(500)]
//        public async Task<IActionResult> RemoveEphemeralCredentials(
//            [FromRoute] string userId,
//            [FromRoute] string jobId)
//        {
//            try
//            {
//                var credentialPath = $"ephemeral/{userId}/{jobId}";
//                var result = await _vaultService.RemoveCredentialAsync(credentialPath);

//                if (result)
//                {
//                    Log.Information("Removed ephemeral credentials for user {UserId}, job {JobId}", userId, jobId);
//                    return Ok(new { Message = "Ephemeral credentials removed successfully" });
//                }

//                return NotFound(new { Message = "Credentials not found or already removed" });
//            }
//            catch (Exception ex)
//            {
//                Log.Error(ex, "Error removing ephemeral credentials for user {UserId}, job {JobId}", userId, jobId);
//                return StatusCode(500, new { Message = "Internal server error", Error = ex.Message });
//            }
//        }

//        private static string GenerateSecurePassword(int length = 16)
//        {
//            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789!@#$%^&*";
//            var random = new Random();
//            return new string(Enumerable.Repeat(chars, length)
//                .Select(s => s[random.Next(s.Length)]).ToArray());
//        }

//        public class CreateEphemeralCredentialRequest
//        {
//            [Required]
//            public string UserId { get; set; } = string.Empty;

//            [Required]
//            public string JobId { get; set; } = string.Empty;

//            [Required]
//            public string ServiceType { get; set; } = string.Empty; // "postgres", "trino", "minio"

//            public string ServerAddress { get; set; } = string.Empty;

//            public int Port { get; set; } = 5432;

//            public string DatabaseName { get; set; } = string.Empty;

//            public string BucketName { get; set; } = string.Empty; // For MinIO

//            [Range(1, 168)] // Max 7 days
//            public int ExpirationHours { get; set; } = 24;

//            public string Description { get; set; } = string.Empty;
//        }

//        public class EphemeralCredentialResponse
//        {
//            public bool Success { get; set; }
//            public string CredentialPath { get; set; } = string.Empty;
//            public DateTime ExpiresAt { get; set; }
//            public string Message { get; set; } = string.Empty;
//        }
//    }
//}
