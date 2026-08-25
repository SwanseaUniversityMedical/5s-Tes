using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Serilog;

namespace Agent.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class BucketCleanupController : Controller
    {
        private readonly IDoBucketCleanupWork _bucketCleanupWork;

        public BucketCleanupController(IDoBucketCleanupWork bucketCleanupWork)
        {
            _bucketCleanupWork = bucketCleanupWork;
        }

        /// <summary>
        /// Runs the expired-project bucket cleanup immediately, in-request (bypasses the Hangfire
        /// schedule/dashboard). Useful for testing and for on-demand cleanup. Honours the same
        /// eligibility rule as the scheduled job (expiry date + grace window) and the
        /// BucketsCleaned bookkeeping, so it is safe to call repeatedly.
        /// </summary>
        [HttpPost("Run")]
        [Authorize(Roles = "dare-tre-admin")]
        public async Task<IActionResult> Run()
        {
            try
            {
                await _bucketCleanupWork.Execute();
                return Ok("Bucket cleanup run completed. See logs for per-project detail.");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Manual bucket cleanup run failed");
                return StatusCode(500, $"Bucket cleanup run failed: {ex.Message}");
            }
        }
    }
}
