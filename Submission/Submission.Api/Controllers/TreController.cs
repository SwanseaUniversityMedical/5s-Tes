using Newtonsoft.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Serilog;
using Microsoft.AspNetCore.Authentication;
using FiveSafesTes.Core.Models;
using FiveSafesTes.Core.Models.Settings;
using FiveSafesTes.Core.Models.ViewModels;
using Microsoft.EntityFrameworkCore;
using Submission.Api.Repositories.DbContexts;
using Submission.Api.Services;
using Submission.Api.Services.Contract;
using FiveSafesTes.Core.Services;
using System.Text;

namespace Submission.Api.Controllers
{
    [ApiExplorerSettings(GroupName = "v1")]
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class TreController : Controller
    {
        private readonly ApplicationDbContext _DbContext;
        protected readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IKeycloakAdminService _keycloakAdminService;
        private readonly IVaultCredentialsService _vaultCredentialsService;
        private readonly IOnboardingJwtService _onboardingJwtService;
        private readonly SubmissionKeyCloakSettings _keycloakSettings;
        private readonly IConfiguration _configuration;


        public TreController(ApplicationDbContext applicationDbContext, IHttpContextAccessor httpContextAccessor,
            IKeycloakAdminService keycloakAdminService, IVaultCredentialsService vaultCredentialsService,
            IOnboardingJwtService onboardingJwtService, SubmissionKeyCloakSettings keycloakSettings,
            IConfiguration configuration)
        {

            _DbContext = applicationDbContext;
            _httpContextAccessor = httpContextAccessor;
            _keycloakAdminService = keycloakAdminService;
            _vaultCredentialsService = vaultCredentialsService;
            _onboardingJwtService = onboardingJwtService;
            _keycloakSettings = keycloakSettings;
            _configuration = configuration;

        }     

        [Authorize(Roles = "dare-control-admin")]
        [HttpPost("SaveTre")]
        public async Task<IActionResult> SaveTre([FromBody] FormData data)
        {
            try
            {
                Tre tre = JsonConvert.DeserializeObject<Tre>(data.FormIoString);
                tre.Name = tre.Name?.Trim();

                if (_DbContext.Tres.Any(x => x.Name.ToLower() == tre.Name.ToLower().Trim() && x.Id != tre.Id))
                {
                    return BadRequest("Another tre already exists with the same name");
                }

                if (_DbContext.Tres.Any(x => x.AdminUsername.ToLower() == tre.AdminUsername.ToLower() && x.Id != tre.Id))
                {
                    return BadRequest("Another tre already exists with the same admin username");
                }

                if (_DbContext.Tres.Any(x => !string.IsNullOrWhiteSpace(x.About) && x.About.ToLower() == tre.About.ToLower() && x.Id != tre.Id))
                {
                    return BadRequest("Another TRE already exists with the same about field");
                }

                tre.FormData = data.FormIoString;

                var logtype = LogType.AddTre;
                if (tre.Id > 0)
                {
                    if (_DbContext.Tres.Select(x => x.Id == tre.Id).Any())
                    {
                        _DbContext.Tres.Update(tre);
                        logtype = LogType.UpdateTre;
                    }
                    else
                    {
                        _DbContext.Tres.Add(tre);
                    }
                }
                else
                {
                    _DbContext.Tres.Add(tre);
                }
                await _DbContext.SaveChangesAsync();
                await ControllerHelpers.AddAuditLog(logtype, null, null, tre, null, null, _httpContextAccessor, User, _DbContext);

                if (logtype == LogType.AddTre && string.IsNullOrEmpty(tre.KeycloakClientId))
                {
                    try
                    {
                        var (clientUuid, clientId, clientSecret) = await _keycloakAdminService.CreateServiceAccountAsync(tre.Name);

                        tre.KeycloakClientId = clientId;
                        await _DbContext.SaveChangesAsync();

                        await _vaultCredentialsService.AddCredentialAsync($"tre/{tre.Name.ToLower()}/keycloak",
                            new Dictionary<string, object>
                            {
                                { "clientId", clientId },
                                { "clientSecret", clientSecret },
                                { "clientUuid", clientUuid }
                            });

                        Log.Information("{Function} Service account {ClientId} created and stored in Vault for TRE {TreName}",
                            "SaveTre", clientId, tre.Name);
                    }
                    catch (Exception serviceAccountEx)
                    {
                        Log.Error(serviceAccountEx, "{Function} Failed to create service account for TRE {TreName}. TRE was saved but service account needs manual setup.",
                            "SaveTre", tre.Name);
                    }
                }

                return Ok(tre); 
            }
            catch (Exception ex)
            {
                Log.Error(ex, "{Function} Crashed", "SaveTre");
                return StatusCode(500, "An internal server error occurred");
            }
        }
        [HttpGet("GetTresInProject/{projectId}")]
        public List<Tre> GetTresInProject(int projectId)
        {
            try
            {
                List<Tre> treslist = _DbContext.Projects.Where(p => p.Id == projectId).SelectMany(p => p.Tres).ToList();
                return treslist;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "{Function Crashed", "GetTresInProject");
                throw;
            }
        }

        [HttpGet("GetAllTresUI")]
        public async Task<List<TreGetProjectModel>> GetAllTresUI()
        {
            try
            {
                var accessToken = await _httpContextAccessor.HttpContext.GetTokenAsync("access_token");
                var allTres = _DbContext.Tres
                    .AsNoTracking()
                    .Select(tre => new TreGetProjectModel(tre, 0, false))
                    .ToList();

                Log.Information("{Function} Tres retrieved successfully", "GetAllTres");
                return allTres;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "{Function} Crashed", "GetAllTres");
                throw;
            }


        }


        [HttpGet("GetAllTres")]
        public async Task<List<Tre>> GetAllTres()
        {
            try
            {
                var allTres = _DbContext.Tres.ToList();
                
                Log.Information("{Function} Tres retrieved successfully", "GetAllTres");
                return allTres;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "{Function} Crashed", "GetAllTres");
                throw;
            }


        }
        
        [HttpGet("GetATre")]
        public Tre? GetATre(int treId)
        {
            try
            {
                var returned = _DbContext.Tres.Find(treId);
                if (returned == null)
                {
                    return null;
                }

                Log.Information("{Function} Project retrieved successfully", "GetATre");
                return returned;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "{Function} Crashed", "GetATre");
                throw;
            }


        }

        [HttpGet("DownloadConfig/{treId}")]
        public IActionResult DownloadConfig(int treId)
        {
            try
            {
                var tre = _DbContext.Tres.Find(treId);
                if (tre == null)
                {
                    return NotFound($"TRE with id {treId} not found");
                }

                var username = (from x in User.Claims
                                where x.Type == "preferred_username"
                                select x.Value).FirstOrDefault();
                var isSuperAdmin = User.IsInRole("dare-control-admin");
                var isTreAdmin = !string.IsNullOrEmpty(username) &&
                                 !string.IsNullOrEmpty(tre.AdminUsername) &&
                                 string.Equals(tre.AdminUsername, username, StringComparison.OrdinalIgnoreCase);

                if (!isSuperAdmin && !isTreAdmin)
                {
                    Log.Warning("{Function} User {User} is not authorised to download config for TRE {TreName}",
                        "DownloadConfig", username, tre.Name);
                    return Forbid();
                }

                var jwt = _onboardingJwtService.GenerateOnboardingJwt(tre);

                var config = new TreOnboardingConfig
                {
                    TREId = tre.Id,
                    TREName = tre.Name,
                    SubmissionURL = _configuration["SubmissionApiUrl"] ?? string.Empty,
                    KeycloakRealmSettingURL = _keycloakSettings.MetadataAddress,
                    JWT = jwt
                };

                var json = JsonConvert.SerializeObject(config, Formatting.Indented);
                var bytes = Encoding.UTF8.GetBytes(json);
                var fileName = $"tre-onboarding-{tre.Name.ToLower()}.json";

                Log.Information("{Function} Config generated for TRE {TreName} by user {User}",
                    "DownloadConfig", tre.Name, username);

                return File(bytes, "application/json", fileName);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "{Function} Crashed", "DownloadConfig");
                return StatusCode(500, "An internal server error occurred");
            }
        }


    }
}
