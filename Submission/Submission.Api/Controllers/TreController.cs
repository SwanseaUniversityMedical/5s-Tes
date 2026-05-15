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
        private readonly SubmissionKeyCloakSettings _keycloakSettings;
        private readonly IConfiguration _configuration;


        public TreController(ApplicationDbContext applicationDbContext, IHttpContextAccessor httpContextAccessor,
            IKeycloakAdminService keycloakAdminService, IVaultCredentialsService vaultCredentialsService,
            SubmissionKeyCloakSettings keycloakSettings, IConfiguration configuration)
        {

            _DbContext = applicationDbContext;
            _httpContextAccessor = httpContextAccessor;
            _keycloakAdminService = keycloakAdminService;
            _vaultCredentialsService = vaultCredentialsService;
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

                        await _vaultCredentialsService.AddCredentialAsync($"tre/{tre.Name.ToLowerInvariant()}/keycloak",
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
        public async Task<IActionResult> GetAllTres(string? responseType = "full")
        {
          try
          {
            if (string.Equals(responseType, "summary", StringComparison.OrdinalIgnoreCase))
            {
              var summaryTres = await _DbContext.Tres
                .AsNoTracking()
                .Select(t => new Tre.TreSummary()
                {
                  Id = t.Id,
                  Name = t.Name,
                  About = t.About,
                  LastHeartBeatReceived = t.LastHeartBeatReceived,
                  ProjectCount = t.Projects.Count,
                  SubmissionCount = t.Submissions.Count(s => s.ParentId == null),
                })
                .ToListAsync();

              Log.Information("{Function} TRE summaries retrieved successfully", nameof(GetAllTres));
              return Ok(summaryTres);
            }

            var allTres = await _DbContext.Tres.ToListAsync();

            Log.Information("{Function} Tres retrieved successfully", nameof(GetAllTres));
            return Ok(allTres);
          }
          catch (Exception ex)
          {
            Log.Error(ex, "{Function} Crashed", nameof(GetAllTres));
            throw;
          }
        }
        
        [HttpGet("GetATre")]
        public async Task<IActionResult> GetATre(int treId, string? responseType = "full")
        {
            try
            {
              if (string.Equals(responseType, "summary", StringComparison.OrdinalIgnoreCase))
              {
                var tre = await _DbContext.Tres
                  .AsNoTracking()
                  .Where(t => t.Id == treId)
                  .Select(t => new Tre.TreDetailsDto
                  {
                    Id = t.Id,
                    Name = t.Name,
                    About = t.About,
                    LastHeartBeatReceived = t.LastHeartBeatReceived,
                    Projects = t.Projects
                      .Select(p => new Project.ProjectSummary
                      {
                        Id = p.Id,
                        Name = p.Name,
                        StartDate = p.StartDate,
                        EndDate = p.EndDate,
                        ProjectDescription = p.ProjectDescription,
                        SubmissionCount = p.Submissions.Count(s => s.Parent == null),
                        UserCount = p.Users.Count(),
                        TreCount = p.Tres.Count()
                      })
                      .ToList(),
                    Submissions = t.Submissions
                      .Select(s => new Project.ProjectSubmissionDto
                      {
                        Id = s.Id,
                        ParentId = s.ParentId,
                        HasParent = s.ParentId != null,
                        Status = s.Status,
                        StartTime = s.StartTime,
                        EndTime = s.EndTime,
                        TesName = s.TesName,
                        ProjectName = s.Project.Name,
                        SubmittedByName = s.SubmittedBy.Name
                      })
                      .ToList()
                  })
                  .FirstOrDefaultAsync();

                if (tre == null)
                {
                  return NotFound();
                }

                Log.Information("{Function} TRE details retrieved successfully", nameof(GetATre));
                return Ok(tre);
              }
              
              var fullTre = _DbContext.Tres.Find(treId);
              if (fullTre == null)
              {
                return NotFound();
              }

              Log.Information("{Function} TRE retrieved successfully", nameof(GetATre));
              return Ok(fullTre);
              
            }
            catch (Exception ex)
            {
                Log.Error(ex, "{Function} Crashed", nameof(GetATre));
                throw;
            }
        }

        [Authorize(Roles = "dare-tre-admin")]
        [HttpGet("DownloadConfig/{treId}")]
        public async Task<IActionResult> DownloadConfig(int treId)
        {
            try
            {
                var tre = await _DbContext.Tres.FindAsync(treId);
                if (tre == null)
                {
                    return NotFound($"TRE with id {treId} not found");
                }

                // Only the TRE's assigned human admin can download onboarding config.
                var username = (from x in User.Claims
                                where x.Type == "preferred_username"
                                select x.Value).FirstOrDefault();
                var isTreAdmin = !string.IsNullOrEmpty(username) &&
                                 !string.IsNullOrEmpty(tre.AdminUsername) &&
                                 string.Equals(tre.AdminUsername, username, StringComparison.OrdinalIgnoreCase);

                if (!isTreAdmin)
                {
                    Log.Warning("{Function} User {User} is not authorised to download config for TRE {TreName}",
                        "DownloadConfig", username, tre.Name);
                    return Forbid();
                }

                if (string.IsNullOrEmpty(tre.KeycloakClientId))
                {
                    Log.Error("{Function} TRE {TreName} has no Keycloak service account configured",
                        "DownloadConfig", tre.Name);
                    return StatusCode(500, "TRE has no Keycloak service account configured");
                }

                // Service-account credentials are persisted under a TRE-specific vault path.
                var vaultPath = $"tre/{tre.Name.ToLowerInvariant()}/keycloak";
                var credential = await _vaultCredentialsService.GetCredentialAsync(vaultPath);
                if (credential == null || !credential.TryGetValue("clientSecret", out var secretObj) ||
                    string.IsNullOrEmpty(secretObj?.ToString()))
                {
                    Log.Error("{Function} Could not retrieve service account secret from Vault for TRE {TreName}",
                        "DownloadConfig", tre.Name);
                    return StatusCode(500, "Service account secret not found in Vault");
                }

                var clientSecret = secretObj!.ToString()!;
                var serviceAccountJwt = await _keycloakAdminService.GetServiceAccountTokenAsync(
                    tre.KeycloakClientId, clientSecret);

                var config = new TreOnboardingConfig
                {
                    TREId = tre.Id,
                    TREName = tre.Name,
                    SubmissionURL = _configuration["SubmissionApiUrl"] ?? string.Empty,
                    KeycloakRealmSettingURL = _keycloakSettings.MetadataAddress,
                    JWT = serviceAccountJwt,
                    IsConfigurationImported = true
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
