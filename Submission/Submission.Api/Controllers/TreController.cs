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
                List<Tre> treslist = _DbContext.Projects
                    .AsNoTracking()
                    .Where(p => p.Id == projectId)
                    .SelectMany(p => p.Tres)
                    .Select(t => new Tre
                    {
                        Id = t.Id,
                        Name = t.Name,
                        LastHeartBeatReceived = t.LastHeartBeatReceived,
                        About = t.About,
                        FormData = t.FormData
                    })
                    .ToList();
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
                var accessToken = await _httpContextAccessor.HttpContext.GetTokenAsync("access_token");
                var allTres = _DbContext.Tres
                    .AsNoTracking()
                    .Select(t => new Tre
                    {
                        Id = t.Id,
                        Name = t.Name,
                        LastHeartBeatReceived = t.LastHeartBeatReceived,
                        About = t.About,
                        FormData = t.FormData,
                        Projects = t.Projects.Select(p => new Project { Id = p.Id }).ToList(),
                        Submissions = t.Submissions.Select(s => new FiveSafesTes.Core.Models.Submission { Id = s.Id }).ToList()
                    })
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
        
        [HttpGet("GetATre")]
        public Tre? GetATre(int treId)
        {
            try
            {
                var returned = _DbContext.Tres
                    .AsNoTracking()
                    .Where(x => x.Id == treId)
                    .Select(t => new Tre
                    {
                        Id = t.Id,
                        Name = t.Name,
                        LastHeartBeatReceived = t.LastHeartBeatReceived,
                        About = t.About,
                        FormData = t.FormData,
                        Projects = t.Projects.Select(p => new Project
                        {
                            Id = p.Id,
                            Name = p.Name,
                            StartDate = p.StartDate,
                            EndDate = p.EndDate,
                            Users = p.Users.Select(u => new User { Id = u.Id }).ToList(),
                            Tres = p.Tres.Select(tr => new Tre { Id = tr.Id }).ToList(),
                            Submissions = p.Submissions.Select(s => new FiveSafesTes.Core.Models.Submission
                            {
                                Id = s.Id,
                                ParentId = s.ParentId
                            }).ToList()
                        }).ToList(),
                        Submissions = t.Submissions.Select(s => new FiveSafesTes.Core.Models.Submission
                        {
                            Id = s.Id,
                            ParentId = s.ParentId,
                            Parent = s.ParentId == null ? null : new FiveSafesTes.Core.Models.Submission { Id = s.ParentId.Value },
                            TesName = s.TesName,
                            Status = s.Status,
                            StartTime = s.StartTime,
                            EndTime = s.EndTime,
                            Project = s.Project == null ? null : new Project
                            {
                                Id = s.Project.Id,
                                Name = s.Project.Name,
                                OutputBucket = s.Project.OutputBucket
                            },
                            SubmittedBy = s.SubmittedBy == null ? null : new User
                            {
                                Id = s.SubmittedBy.Id,
                                Name = s.SubmittedBy.Name,
                                FullName = s.SubmittedBy.FullName
                            }
                        }).ToList()
                    })
                    .FirstOrDefault();
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


    }
}
