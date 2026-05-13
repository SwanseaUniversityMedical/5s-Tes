using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Microsoft.AspNetCore.Authorization;
using Serilog;
using FiveSafesTes.Core.Models;
using FiveSafesTes.Core.Models.ViewModels;
using Microsoft.EntityFrameworkCore;
using Submission.Api.Repositories.DbContexts;
using Submission.Api.Services;
using Submission.Api.Services.Contract;

namespace Submission.Api.Controllers
{    
  
    [ApiExplorerSettings(GroupName = "v1")]
    [Route("api/[controller]")]
    [Authorize]
    public class UserController : ControllerBase
    {
        private readonly ApplicationDbContext _DbContext;
        private readonly IKeycloakMinioUserService _keycloakMinioUserService;
        protected readonly IHttpContextAccessor _httpContextAccessor;

        public UserController(ApplicationDbContext applicationDbContext, IKeycloakMinioUserService keycloakMinioUserService, IHttpContextAccessor httpContextAccessor)
        {

            _DbContext = applicationDbContext;
            _keycloakMinioUserService = keycloakMinioUserService;
            _httpContextAccessor = httpContextAccessor;

        }

        [Authorize(Roles = "dare-control-admin")]
        [HttpPost("SaveUser")]
        public async Task<User> SaveUser([FromBody] FormData data) 
        {
            if (!ModelState.IsValid) // SonarQube security
            {
                return null;
            }

            try
            {

                User userData = JsonConvert.DeserializeObject<User>(data.FormIoString);
                userData.FullName = userData.FullName.Trim();
                userData.Name = userData.Name.Trim();
                userData.Email = userData.Email.Trim();
                userData.FormData = data.FormIoString;


                if (_DbContext.Users.Any(x => x.Name.ToLower() == userData.Name.ToLower().Trim() && x.Id != userData.Id))
                {
                    
                    return new User() { Error = true, ErrorMessage = "Another user already exists with the same name" };
                }

                var logtype = LogType.AddUser;

                if (userData.Id > 0)
                {
                    if (_DbContext.Users.Select(x => x.Id == userData.Id).Any())
                    {
                        _DbContext.Users.Update(userData);
                        logtype = LogType.AddUser;
                    }
                    else
                    {
                        _DbContext.Users.Add(userData);
                    }
                }
                else
                    _DbContext.Users.Add(userData);

                await _DbContext.SaveChangesAsync();

                
                await ControllerHelpers.AddAuditLog(logtype, userData, null, null, null, null, _httpContextAccessor, User, _DbContext);

                return userData;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "{Function} Crashed", "SaveUser");
                return new User(); ;
                throw;
            }

            
        }

        
        [HttpGet("GetUser")]
        public User? GetUser(int userId)
        {
            if (!ModelState.IsValid) // SonarQube security
            {
                return null;
            }

            try
            {
                var returned = _DbContext.Users.Find(userId);
                if (returned == null)
                {
                    return null;
                }
                
                Log.Information("{Function} User retrieved successfully", "GetUser");
                return returned;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "{Function} Crashed", "GetUser");
                throw;
            }

            
        }

        [HttpGet("GetUserDetails")]
        public async Task<ActionResult<User.UserDetailsDto>> GetUserDetails(int userId)
        {
            if (!ModelState.IsValid) // SonarQube security
            {
                return BadRequest();
            }

            try
            {
                var user = await _DbContext.Users
                    .AsNoTracking()
                    .Where(u => u.Id == userId)
                    .Select(u => new User.UserDetailsDto
                    {
                        Id = u.Id,
                        Name = u.Name,
                        FullName = u.FullName,
                        Biography = u.Biography,
                        Organisation = u.Organisation,
                        Projects = u.Projects
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
                        Submissions = u.Submissions
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

                if (user == null)
                {
                    return NotFound();
                }

                var userProjectIds = user.Projects.Select(p => p.Id).ToHashSet();

                user.ProjectsNotInUser = await _DbContext.Projects
                    .AsNoTracking()
                    .Where(p => !userProjectIds.Contains(p.Id))
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
                    .ToListAsync();

                Log.Information("{Function} User details retrieved successfully", nameof(GetUserDetails));
                return Ok(user);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "{Function} Crashed", nameof(GetUserDetails));
                throw;
            }
        }

        
        [HttpGet("GetAllUsersUI")]
        public List<UserGetProjectModel> GetAllUsersUI()
        {
            try
            {
                List<UserGetProjectModel> allUsers = _DbContext.Users
                    .AsNoTracking()
                    .Select(user => new UserGetProjectModel(user))
                    .ToList();

                Log.Information("{Function} Users retrieved successfully", "GetAllUsers");
                return allUsers;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "{Function} Crash", "GetAllUsers");
                throw;
            }
        }

        
        [HttpGet("GetAllUsers")]
        public async Task<IActionResult> GetAllUsers(string? responseType = "full")
        {
            try
            {
              if (string.Equals(responseType, "summary", StringComparison.OrdinalIgnoreCase))
              {
                var summaryUsers = await _DbContext.Users
                  .AsNoTracking()
                  .Select(u => new User.UserSummary()
                  {
                    Id = u.Id,
                    Name = u.Name,
                    FullName = u.FullName,
                    ProjectCount = u.Projects.Count,
                    SubmissionCount = u.Submissions.Count(s => s.Parent == null),
                  })
                  .ToListAsync();

                Log.Information("{Function} Project summaries retrieved successfully", "GetAllProjects");
                return Ok(summaryUsers);
              }
              
              var allUsers = await _DbContext.Users.ToListAsync();
              
               Log.Information("{Function} Users retrieved successfully", "GetAllUsers");
                return Ok(allUsers);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "{Function} Crash", "GetAllUsers");
                throw;
            }
        }
       
        [Authorize(Roles = "dare-control-admin")]
        [HttpPost("AddProjectMembership")]
        public async Task<ProjectUser?> AddProjectMembership([FromBody]ProjectUser model)
        {
            if (!ModelState.IsValid) // SonarQube security
            {
                return null;
            }

            try
            {
                var user = _DbContext.Users.FirstOrDefault(x => x.Id == model.UserId);
                if (user == null)
                {
                    Log.Error("{Function} Invalid user id {UserId}", "AddProjectMembership", model.UserId);
                    return null;
                }

                var project = _DbContext.Projects.FirstOrDefault(x => x.Id == model.ProjectId);
                if (project == null)
                {
                    Log.Error("{Function} Invalid project id {UserId}", "AddProjectMembership", model.ProjectId);
                    return null;
                }

               
                if (user.Projects.Any(x => x == project))
                {
                    Log.Error("{Function} User {UserName} is already on {ProjectName}", "AddProjectMembership", user.Name, project.Name);
                    return null;
                }
                user.Projects.Add(project);

                await _DbContext.SaveChangesAsync();
                await ControllerHelpers.AddUserToMinioBucket(user, project, _httpContextAccessor, "policy", _keycloakMinioUserService, User, _DbContext);
                await ControllerHelpers.AddAuditLog(LogType.AddUserToProject, user, project, null, null, null, _httpContextAccessor, User, _DbContext);
                
                Log.Information("{Function} Added User {UserName} to {ProjectName}", "AddProjectMembership", user.Name, project.Name);

                return model;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "{Function} Crash", "AddProjectMembership");
                throw;
            }


        }
        [Authorize(Roles = "dare-control-admin")]
        [HttpPost("RemoveProjectMembership")]
        public async Task<ProjectUser?> RemoveProjectMembership([FromBody] ProjectUser model)
        {
            if (!ModelState.IsValid) // SonarQube security
            {
                return null;
            }

            try
            {
                var user = _DbContext.Users.FirstOrDefault(x => x.Id == model.UserId);
                if (user == null)
                {
                    Log.Error("{Function} Invalid user id {UserId}", "RemoveProjectMembership", model.UserId);
                    return null;
                }

                var project = _DbContext.Projects.FirstOrDefault(x => x.Id == model.ProjectId);
                if (project == null)
                {
                    Log.Error("{Function} Invalid project id {UserId}", "RemoveProjectMembership", model.ProjectId);
                    return null;
                }

                if (!user.Projects.Any(x => x == project))
                {
                    Log.Error("{Function} User {UserName} is not in the {ProjectName}", "RemoveProjectMembership", user.Name, project.Name);
                    return null;
                }
                user.Projects.Remove(project);
                await _DbContext.SaveChangesAsync();
                await ControllerHelpers.RemoveUserFromMinioBucket(user, project, _httpContextAccessor, "policy", _keycloakMinioUserService, User, _DbContext);
                await ControllerHelpers.AddAuditLog(LogType.RemoveUserFromProject, user, project, null, null, null, _httpContextAccessor, User, _DbContext);
                
                Log.Information("{Function} Added Project {ProjectName} to {UserName}", "RemoveProjectMembership", project.Name, user.Name);
                return model;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "{Function} Crash", "RemoveUserMembership");
                throw;
            }


        }
    }
}
