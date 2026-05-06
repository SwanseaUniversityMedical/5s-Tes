using FiveSafesTes.Core.Models;
using FiveSafesTes.Core.Models.ViewModels;
using FiveSafesTes.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Submission.Api.Repositories.DbContexts;

namespace Submission.Api.Controllers
{
    [ApiExplorerSettings(GroupName = "v1")]
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class OnboardingController : Controller
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly IVaultCredentialsService _vaultCredentialsService;

        public OnboardingController(
            ApplicationDbContext dbContext,
            IVaultCredentialsService vaultCredentialsService)
        {
            _dbContext = dbContext;
            _vaultCredentialsService = vaultCredentialsService;
        }

        [HttpPost("RetrieveCredentials")]
        public async Task<IActionResult> RetrieveCredentials()
        {
            try
            {
                // Service-account tokens normally expose client identity in azp.
                var clientId = User.FindFirst("azp")?.Value ?? User.FindFirst("client_id")?.Value;
                if (string.IsNullOrWhiteSpace(clientId))
                {
                    return Unauthorized(new { error = "Token does not contain a service account client id claim" });
                }

                // jti is a unique token id; we persist it to prevent it from being used again.
                var jti = User.FindFirst("jti")?.Value;
                if (string.IsNullOrWhiteSpace(jti))
                {
                    return Unauthorized(new { error = "Token does not contain jti claim" });
                }

                var alreadyUsed = await _dbContext.UsedOnboardingJtis.AnyAsync(x => x.Jti == jti);
                if (alreadyUsed)
                {
                    Log.Warning("{Function} Replay attempt for jti {Jti}", "RetrieveCredentials", jti);
                    return Unauthorized(new { error = "Token has already been used" });
                }

                // Client id maps to the TRE's provisioned Keycloak service account.
                var tre = await _dbContext.Tres.FirstOrDefaultAsync(x => x.KeycloakClientId == clientId);
                if (tre == null)
                {
                    Log.Warning("{Function} TRE for clientId {ClientId} was not found", "RetrieveCredentials", clientId);
                    return NotFound(new { error = "TRE not found for provided service account" });
                }

                var vaultPath = $"tre/{tre.Name.ToLowerInvariant()}/keycloak";
                Dictionary<string, object> creds;
                try
                {
                    creds = await _vaultCredentialsService.GetCredentialAsync(vaultPath);
                }
                catch (Exception vaultEx)
                {
                    Log.Error(vaultEx, "{Function} Failed to read credentials from Vault for TRE {TreName}",
                        "RetrieveCredentials", tre.Name);
                    return StatusCode(500, new { error = "Could not retrieve credentials" });
                }

                if (creds == null
                    || !creds.TryGetValue("clientId", out var clientIdObj)
                    || !creds.TryGetValue("clientSecret", out var clientSecretObj))
                {
                    Log.Error("{Function} Vault did not contain expected credentials for TRE {TreName}",
                        "RetrieveCredentials", tre.Name);
                    return StatusCode(500, new { error = "Credentials are not provisioned for this TRE" });
                }

                _dbContext.UsedOnboardingJtis.Add(new UsedOnboardingJti
                {
                    Jti = jti,
                    TreId = tre.Id,
                    UsedAt = DateTime.UtcNow
                });
                await _dbContext.SaveChangesAsync();

                Log.Information("{Function} Onboarding credentials issued for TRE {TreName} (clientId={ClientId}, jti={Jti})",
                    "RetrieveCredentials", tre.Name, clientId, jti);

                return Ok(new OnboardingCredentialsResponse
                {
                    TreId = tre.Id,
                    TreName = tre.Name,
                    ClientId = clientIdObj?.ToString() ?? string.Empty,
                    ClientSecret = clientSecretObj?.ToString() ?? string.Empty
                });
            }
            catch (Exception ex)
            {
                Log.Error(ex, "{Function} Crashed", "RetrieveCredentials");
                return StatusCode(500, new { error = "An internal server error occurred" });
            }
        }
    }
}
