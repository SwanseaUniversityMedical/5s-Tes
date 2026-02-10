using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Newtonsoft.Json;
using Serilog;
using Microsoft.AspNetCore.Authentication;
using System.Text.RegularExpressions;
using FiveSafesTes.Core.Models;
using FiveSafesTes.Core.Models.APISimpleTypeReturns;
using FiveSafesTes.Core.Models.ViewModels;
using FiveSafesTes.Core.Services;
using Microsoft.EntityFrameworkCore;
using Submission.Api.Repositories.DbContexts;
using Submission.Api.Services;
using Submission.Api.Services.Contract;

namespace Submission.Api.Controllers
{

    [ApiController]
    [Route("api/[controller]")]
    [Authorize]


    public class ProjectController : Controller
    {

        private readonly ApplicationDbContext _DbContext;
        private readonly MinioSettings _minioSettings;
        private readonly IMinioHelper _minioHelper;
        private readonly IKeycloakMinioUserService _keycloakMinioUserService;
        protected readonly IHttpContextAccessor _httpContextAccessor;

        public ProjectController(ApplicationDbContext applicationDbContext, MinioSettings minioSettings, IMinioHelper minioHelper, IKeycloakMinioUserService keycloakMinioUserService, IHttpContextAccessor httpContextAccessor)
        {

            _DbContext = applicationDbContext;
            _minioSettings = minioSettings;
            _minioHelper = minioHelper;
            _keycloakMinioUserService = keycloakMinioUserService;
            _httpContextAccessor = httpContextAccessor;
        }

        [Authorize(Roles = "dare-control-admin")]
        [HttpPost("SaveProject")]
        public async Task<Project?> SaveProject([FromBody] FormData data)
        {
            Project project = null;
            try
            {
                project = JsonConvert.DeserializeObject<Project>(data.FormIoString);
                //2023-06-01 14:30:00 use this as the datetime
           

                string input = project.Name.Trim();
                string pattern = @"[^a-zA-Z0-9]"; // exclude everything but letters and numbers
                string result = Regex.Replace(input, pattern, "");
                project.Name = result;
                project.StartDate = project.StartDate.ToUniversalTime();
                project.EndDate = project.EndDate.ToUniversalTime();
                project.ProjectDescription = project.ProjectDescription.Trim();
               project.FormData = data.FormIoString;
                

                if (_DbContext.Projects.Any(x => x.Name.ToLower() == project.Name.ToLower().Trim() && x.Id != project.Id))
                {

                    return new Project() { Error = true, ErrorMessage = "Another project already exists with the same name" };
                }

                if (project.Id == 0)
                {
                    try
                    {
                        _DbContext.Projects.Add(project);
                        await _DbContext.SaveChangesAsync();

                        project.SubmissionBucket = GenerateRandomName(project.Id.ToString()) + "submission".Replace("_", "");
                        project.OutputBucket = GenerateRandomName(project.Id.ToString()) + "output".Replace("_", ""); ;
                        var submissionBucket = await _minioHelper.CreateBucket(project.SubmissionBucket);
                        if (!submissionBucket)
                        {
                            Log.Error("{Function} S3GetListObjects: Failed to create bucket {name}.", "SaveProject", project.SubmissionBucket);
                            throw new Exception("{Function} CreateBucketPolicy: Failed to create policy for bucket {name}.");
                        }
                        else
                        {
                            var submistionBucketPolicy = await _minioHelper.CreateBucketPolicy(project.SubmissionBucket);
                            if (!submistionBucketPolicy)
                            {
                                Log.Error("{Function} CreateBucketPolicy: Failed to create policy for bucket {name}.", "SaveProject", project.SubmissionBucket);
                                throw new Exception("{Function} CreateBucketPolicy: Failed to create policy for bucket {name}.");
                            }
                        }
                        var outputBucket = await _minioHelper.CreateBucket(project.OutputBucket);
                        if (!outputBucket)
                        {
                            Log.Error("{Function} S3GetListObjects: Failed to create bucket {name}.", "SaveProject", project.OutputBucket);
                            throw new Exception("{Function} CreateBucketPolicy: Failed to create policy for bucket {name}.");

                        }
                        else
                        {
                            var outputBucketPolicy = await _minioHelper.CreateBucketPolicy(project.OutputBucket);
                            if (!outputBucketPolicy)
                            {
                                Log.Error("{Function} CreateBucketPolicy: Failed to create policy for bucket {name}.", "SaveProject", project.OutputBucket);
                                throw new Exception("{Function} CreateBucketPolicy: Failed to create policy for bucket {name}.");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        if (project != null)
                        {
                            if (project.Id != 0)
                            {
                                _DbContext.Projects.Remove(project);
                                await _DbContext.SaveChangesAsync();
                            }
                        }
                        Log.Error(ex, "{Function} Crash", "SaveProject");
                        var errorModel = new Project();
                        errorModel.FormData = ex.ToString();
                        return errorModel;
                    }
                }

                var logtype = LogType.AddProject;

                if (project.Id > 0)
                {
                    if (_DbContext.Projects.Select(x => x.Id == project.Id).Any())
                    {
                        _DbContext.Projects.Update(project);
                        logtype = LogType.UpdateProject;
                    }
                    else
                    {
                        _DbContext.Projects.Add(project);
                    }
                }
                else
                {
                    _DbContext.Projects.Add(project);

                }
                await _DbContext.SaveChangesAsync();
                await ControllerHelpers.AddAuditLog(logtype, null, project, null, null, null, _httpContextAccessor, User, _DbContext);
                
                
                
                Log.Information("{Function} Projects added successfully", "CreateProject");
                return project;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "{Function} Crash", "SaveProject");
                var errorModel = new Project();
                return errorModel;
                throw;
            }


        }

        [Authorize(Roles = "dare-control-admin")]
        [HttpPost("CheckUserExists")]
        public async Task<ProjectUser?> CheckUserExists(ProjectUser model)
        {
            var accessToken = await _httpContextAccessor.HttpContext.GetTokenAsync("access_token");
            var user = _DbContext.Users.FirstOrDefault(x => x.Id == model.UserId);
            if (user == null)
            {
                return model;
            }
            var userId = await _keycloakMinioUserService.GetUserIDAsync(accessToken, user.Name.ToString());
            if (userId == "")
            {
                var newUser=new ProjectUser();
                return newUser;
            }
            return model;
        }

        [Authorize(Roles = "dare-control-admin")]
        [HttpPost("AddUserMembership")]
        public async Task<ProjectUser?> AddUserMembership(ProjectUser model)
        {
            try
            {
                var user = _DbContext.Users.FirstOrDefault(x => x.Id == model.UserId);
                if (user == null)
                {
                    Log.Error("{Function} Invalid user id {UserId}", "AddUserMembership", model.UserId);
                    return null;
                }

                var project = _DbContext.Projects.FirstOrDefault(x => x.Id == model.ProjectId);
                if (project == null)
                {
                    Log.Error("{Function} Invalid project id {UserId}", "AddUserMembership", model.ProjectId);
                    return null;
                }

                if (project.Users.Any(x => x == user))
                {
                    Log.Error("{Function} User {UserName} is already on {ProjectName}", "AddUserMembership", user.Name, project.Name);
                    return null;
                }
                
                project.Users.Add(user);
                await _DbContext.SaveChangesAsync();
                await ControllerHelpers.AddUserToMinioBucket(user, project, _httpContextAccessor, _minioSettings.AttributeName, _keycloakMinioUserService, User, _DbContext );
                await ControllerHelpers.AddAuditLog(LogType.AddUserToProject, user, project, null, null, null, _httpContextAccessor, User, _DbContext);
                Log.Information("{Function} Added User {UserName} to {ProjectName}", "AddUserMembership", user.Name, project.Name);
                return model;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "{Function} Crash", "AddUserMembership");
                throw;
            }


        }

        

        [Authorize(Roles = "dare-control-admin")]
        [HttpPost("RemoveUserMembership")]
        public async Task<ProjectUser?> RemoveUserMembership(ProjectUser model)
        {
            try
            {
                var user = _DbContext.Users.FirstOrDefault(x => x.Id == model.UserId);
                if (user == null)
                {
                    Log.Error("{Function} Invalid user id {UserId}", "RemoveUserMembership", model.UserId);
                    return null;
                }

                var project = _DbContext.Projects.FirstOrDefault(x => x.Id == model.ProjectId);
                if (project == null)
                {
                    Log.Error("{Function} Invalid project id {UserId}", "RemoveUserMembership", model.ProjectId);
                    return null;
                }

                if (!project.Users.Any(x => x == user))
                {
                    Log.Error("{Function} User {UserName} is not in the {ProjectName}", "RemoveUserMembership", user.Name, project.Name);
                    return null;
                }

                project.Users.Remove(user);
                await _DbContext.SaveChangesAsync();
                await ControllerHelpers.RemoveUserFromMinioBucket(user, project, _httpContextAccessor, _minioSettings.AttributeName, _keycloakMinioUserService, User, _DbContext);

                await ControllerHelpers.AddAuditLog(LogType.RemoveUserFromProject, user, project, null, null, null, _httpContextAccessor, User, _DbContext);
                
                Log.Information("{Function} Added User {UserName} to {ProjectName}", "RemoveUserMembership", user.Name, project.Name);
                return model;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "{Function} Crash", "RemoveUserMembership");
                throw;
            }


        }

        

        [Authorize(Roles = "dare-control-admin")]
        [HttpPost("AddTreMembership")]
        public async Task<ProjectTre?> AddTreMembership(ProjectTre model)
        {
            try
            {
                var tre = _DbContext.Tres.FirstOrDefault(x => x.Id == model.TreId);
                if (tre == null)
                {
                    Log.Error("{Function} Invalid tre id {UserId}", "AddTreMembership", model.TreId);
                    return null;
                }

                var project = _DbContext.Projects.FirstOrDefault(x => x.Id == model.ProjectId);
                if (project == null)
                {
                    Log.Error("{Function} Invalid project id {UserId}", "AddTreMembership", model.ProjectId);
                    return null;
                }

                if (project.Tres.Any(x => x == tre))
                {
                    Log.Error("{Function} Tre {Tre} is already on {ProjectName}", "AddTreMembership", tre.Name, project.Name);
                    return null;
                }

                project.Tres.Add(tre);

                await _DbContext.SaveChangesAsync();
                await ControllerHelpers.AddAuditLog(LogType.AddTreToProject, null, project, tre, null, null, _httpContextAccessor, User, _DbContext);
                Log.Information("{Function} Added Tre {Tre} to {ProjectName}", "AddTreMembership", tre.Name, project.Name);
                return model;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "{Function} Crash", "AddTreMembership");
                throw;
            }


        }

        [Authorize(Roles = "dare-control-admin")]
        [HttpPost("RemoveTreMembership")]
        public async Task<ProjectTre?> RemoveTreMembership(ProjectTre model)
        {
            try
            {
                var tre = _DbContext.Tres.FirstOrDefault(x => x.Id == model.TreId);
                if (tre == null)
                {
                    Log.Error("{Function} Invalid tre id {UserId}", "AddTreMembership", model.TreId);
                    return null;
                }

                var project = _DbContext.Projects.FirstOrDefault(x => x.Id == model.ProjectId);
                if (project == null)
                {
                    Log.Error("{Function} Invalid project id {UserId}", "AddTreMembership", model.ProjectId);
                    return null;
                }

                if (!project.Tres.Any(x => x == tre))
                {
                    Log.Error("{Function} Tre {Tre} is already on {ProjectName}", "AddTreMembership", tre.Name, project.Name);
                    return null;
                }

                project.Tres.Remove(tre);
                
                await _DbContext.SaveChangesAsync();
                await ControllerHelpers.AddAuditLog(LogType.RemoveTreFromProject, null, project, tre, null, null, _httpContextAccessor, User, _DbContext);
                Log.Information("{Function} Added Tre {Tre} to {ProjectName}", "AddTreMembership", tre.Name, project.Name);
                return model;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "{Function} Crash", "AddTreMembership");
                throw;
            }


        }

        
        [HttpGet("GetProjectUI")]
        public SubmissionGetProjectModel? GetProjectUI(int projectId)
        {
            try
            {
                var returned = _DbContext.Projects.Find(projectId);
                if (returned == null)
                {
                    return null;
                }

                Log.Information("{Function} Project retrieved successfully", "GetProject");
                var Users = _DbContext.Users.ToList();
                _DbContext.Tres.ToList();
                return new SubmissionGetProjectModel(returned, _DbContext.Users, _DbContext.Tres);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "{Function} Crashed", "GetProject");
                throw;
            }


        }
        
        [HttpGet("GetProject")]
        public Project? GetProject(int projectId)
        {
            try
            {
                var returned = _DbContext.Projects.Find(projectId);
                if (returned == null)
                {
                    return null;
                }

                Log.Information("{Function} Project retrieved successfully", "GetProject");
                return returned;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "{Function} Crashed", "GetProject");
                throw;
            }


        }

        [HttpGet("GetAllProjects")]
        public List<Project> GetAllProjects()
        {
            try
            {
                //TODO - use User.Identity.IsAuthenticated to alter list returned : embargoed etc

                var allProjects = _DbContext.Projects
                    .ToList();


                Log.Information("{Function} Projects retrieved successfully", "GetAllProjects");
                return allProjects;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "{Function} Crashed", "GetAllProjects");
                throw;
            }


        }


        [HttpGet("GetAllProjectsForTre")]
        [Authorize(Roles = "dare-tre-admin")]
        public List<Project> GetAllProjectsForTre()
        {
            try
            {

                var tre = ControllerHelpers.GetUserTre(User, _DbContext);

                var allProjects = tre.Projects;

                Log.Information("{Function} Projects retrieved successfully", "GetAllProjectsForTre");
                return allProjects;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "{Function} Crashed", "GetAllProjectsForTre");
                throw;
            }


        }

        


        [HttpPost("SyncTreProjectDecisions")]
        [Authorize(Roles = "dare-tre-admin")]
        public BoolReturn SyncTreProjectDecisions([FromBody] List<ProjectTreDecisionsDTO> decisions)
        {
            try
            {
                Log.Information("SyncTreProjectDecisions called with  " + decisions.Count);

                var result = new BoolReturn();
                var tre = ControllerHelpers.GetUserTre(User, _DbContext);

                foreach (var item in decisions)
                {
                    Log.Information("SyncTreProjectDecisions item > ProjectId  " + item.ProjectId  + " Decision  > " + item.Decision);

                    var dbproj = _DbContext.Projects.FirstOrDefault(x => x.Id == item.ProjectId);
                    if (dbproj == null)
                    {
                        Log.Error($"no Projects with ID of {item.ProjectId}");
                        continue;
                    }
                    var tredecision = _DbContext.ProjectTreDecisions.FirstOrDefault(x => x.SubmissionProj == dbproj && x.Tre == tre);
                    if (tredecision == null)
                    {
                        Log.Information("SyncTreProjectDecisions add new  tredecision " + decisions.Count);

                        tredecision = new ProjectTreDecision()
                        {
                            SubmissionProj = dbproj,
                            Tre = tre,
                        };
                        _DbContext.ProjectTreDecisions.Add(tredecision);
                    }
                    tredecision.Decision = item.Decision;
                }
                _DbContext.SaveChanges();


                result.Result = true;
                Log.Information("{Function} Tre {TreName} decisions synched", "SyncTreProjectDecisions", tre.Name);
                return result;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "{Function} Crashed", "SyncTreProjectDecisions");
                throw;
            }


        }

        [HttpPost("SyncTreMembershipDecisions")]
        [Authorize(Roles = "dare-tre-admin")]
        public BoolReturn SyncTreMembershipDecisions([FromBody] List<MembershipTreDecisionDTO> decisions)
        {
            var aresult = new BoolReturn();
            aresult.Result = true;
            return aresult;
            try
            {
                var result = new BoolReturn();
                var usersName = (from x in User.Claims where x.Type == "preferred_username" select x.Value).First();
                var tre = ControllerHelpers.GetUserTre(User, _DbContext);

                foreach (var item in decisions)
                {
                    var dbproj = _DbContext.Projects.First(x => x.Id == item.ProjectId);
                    var dbuser = _DbContext.Users.First(x => x.Id == item.UserId);
                    var tredecision = _DbContext.MembershipTreDecisions.FirstOrDefault(x => x.SubmissionProj == dbproj && x.User == dbuser && x.Tre == tre);
                    if (tredecision == null)
                    {
                        tredecision = new MembershipTreDecision()
                        {
                            SubmissionProj = dbproj,
                            User = dbuser,
                            Tre = tre,
                        };
                        _DbContext.MembershipTreDecisions.Add(tredecision);
                    }
                    tredecision.Decision = item.Decision;
                }
                _DbContext.SaveChanges();


                result.Result = true;
                Log.Information("{Function} Tre {TreName} membership decisions synched", "SyncTreMembershipDecisions", tre.Name);
                return result;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "{Function} Crashed", "SyncTreMembershipDecisions");
                throw;
            }


        }

        [HttpGet("GetTresInProject")]
        public List<Tre> GetTresInProject(int projectId)
        {
            try
            {
                List<Tre> tres = _DbContext.Projects.Where(p => p.Id == projectId).SelectMany(p => p.Tres).ToList();

                return tres;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "{Function} Crashed", "GetTresInProject");
                throw;
            }
        }

        private static string GenerateRandomName(string prefix)
        {
            Random random = new Random();
            string randomName = prefix + random.Next(1000, 9999);
            return randomName;
        }

        //For testing FetchAndStoreS3Object
        public class testFetch
        {
            public string url { get; set; }
            public string bucketName { get; set; }
            public string key { get; set; }
        }

        [Authorize(Roles = "dare-control-admin")]
        [HttpPost("TestFetchAndStoreObject")]
        public async Task<IActionResult> TestFetchAndStoreObject(testFetch testf)
        {
            try
            {
                await _minioHelper.FetchAndStoreObject(testf.url, testf.bucketName, testf.key);

                return Ok();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "{Function} Crashed", "TestFetchAandStoreObject");
                throw;
            }
        }
        
        [HttpGet("IsUserOnProject")]
        public bool IsUserOnProject(int projectId, int userId)
        {
            try
            {
                bool isUserOnProject = _DbContext.Projects.Any(p => p.Id == projectId && p.Users.Any(u => u.Id == userId));
                return isUserOnProject;
            }

            catch (Exception ex)
            {
                Log.Error(ex, "{Function} crash", "IsUserOnProject");
                throw;
            }
        }
        [AllowAnonymous]
        [HttpGet("GetMinioEndPoint")]
        public MinioEndpoint? GetMinioEndPoint()
        {

            var minioEndPoint = new MinioEndpoint()
            {
                Url = _minioSettings.AdminConsole,
            };

            return minioEndPoint;
        }

        

        [HttpPost("UploadToMinio")]
        public async Task<BoolReturn> UploadToMinio(string bucketName, IFormFile file)
        {
            if (file == null || file.Length == 0)
                return new BoolReturn() { Result = false };

            try
            {
                var submissionBucket = await _minioHelper.UploadFileAsync(file, bucketName, file.Name);


                return new BoolReturn() { Result = true };
            }
            catch (Exception ex)
            {
                return new BoolReturn() { Result = false };
            }
        }


        private IFormFile ConvertJsonToIFormFile(string fileJson)
        {
            try
            {
                if (string.IsNullOrEmpty(fileJson))
                    return null;
                var fileData = System.Text.Json.JsonSerializer.Deserialize<IFileData>(fileJson);

                var bytes = Convert.FromBase64String(fileData.Content);

                var fileName = fileData.FileName;
                var contentType = fileData.ContentType;

                var formFile = new FormFile(new MemoryStream(bytes), 0, bytes.Length, null, fileName)
                {
                    Headers = new HeaderDictionary(),
                    ContentType = contentType
                };

                return formFile;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "{Function} Crashed", "ConvertJsonToIformFile");
                throw;
            }
        }
        [AllowAnonymous]
        [HttpGet("GetSearchData")]
        public List<Project> GetSearchData(string searchString)
        {
            try
            {

                //List<Project> searchResults = _DbContext.Projects
                //    .Include(c => c.Users)
                //    .Include(c => c.Submissions)
                //     .Include(c => c.Tres)
                //    .Where(c => c.Name.ToLower().Contains(searchString.Trim().ToLower()) ||
                //    c.Users.Any(t => t.Name.ToLower().Contains(searchString.Trim().ToLower())) ||
                //    c.Tres.Any(t => t.Name.ToLower().Contains(searchString.Trim().ToLower())) || c.Submissions.Any(s => s.TesName.Contains(searchString.Trim().ToLower()))).ToList();
                string normalizedSearchString = $"%{searchString.Trim()}%";

                List<Project> searchResults = _DbContext.Projects

                    .Include(c => c.Users)

                    .Include(c => c.Submissions)

                    .Include(c => c.Tres)

                    .Where(c => EF.Functions.Like(c.Name, normalizedSearchString) ||


                                c.Users.Any(t => EF.Functions.Like(t.Name, normalizedSearchString)) ||

                                c.Tres.Any(t => EF.Functions.Like(t.Name, normalizedSearchString)) ||

                                c.Submissions.Any(s => EF.Functions.Like(s.TesName, normalizedSearchString))

                    )

                    .ToList();
                Log.Information("{Function} Search Data retrieved successfully", "GetSearchData");
                return searchResults.ToList();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "{Function} Crash", "GetSearchData");
                throw;
            }

        }


        //End
        
    }
}
