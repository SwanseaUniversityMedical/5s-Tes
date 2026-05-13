using Newtonsoft.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Serilog;
using Microsoft.AspNetCore.Authentication;
using FiveSafesTes.Core.Models;
using FiveSafesTes.Core.Models.ViewModels;
using Microsoft.EntityFrameworkCore;
using Submission.Api.Repositories.DbContexts;
using Submission.Api.Services;

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


        public TreController(ApplicationDbContext applicationDbContext, IHttpContextAccessor httpContextAccessor)
        {

            _DbContext = applicationDbContext;
            _httpContextAccessor= httpContextAccessor;

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

        [HttpGet("GetATreDetails")]
        public async Task<ActionResult<Tre.TreDetailsDto>> GetATreDetails(int treId)
        {
            try
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
                                SubmissionCount = p.Submissions.Count(s => s.ParentId == null),
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

                Log.Information("{Function} TRE details retrieved successfully", nameof(GetATreDetails));
                return Ok(tre);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "{Function} Crashed", nameof(GetATreDetails));
                throw;
            }
        }


    }
}
