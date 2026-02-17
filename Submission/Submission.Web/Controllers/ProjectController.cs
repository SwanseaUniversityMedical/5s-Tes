using System.Diagnostics;
using FiveSafesTes.Core.Models;
using FiveSafesTes.Core.Models.Settings;
using FiveSafesTes.Core.Models.ViewModels;
using FiveSafesTes.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Submission.Web.Models;

namespace Submission.Web.Controllers
{
    [Authorize]
    public class ProjectController : Controller
    {
        private readonly IDareClientHelper _clientHelper;

        private readonly FormIOSettings _formIOSettings;


        private readonly URLSettingsFrontEnd _URLSettingsFrontEnd;

        public ProjectController(IDareClientHelper client, FormIOSettings formIo,
            URLSettingsFrontEnd URLSettingsFrontEnd)
        {
            _clientHelper = client;

            _formIOSettings = formIo;

            _URLSettingsFrontEnd = URLSettingsFrontEnd;
        }

        private bool IsUserOnProject(SubmissionGetProjectModel proj)
        {
            if (User.IsInRole("dare-control-admin"))
            {
                return true;
            }

            var usersName = "";
            usersName = (from x in User.Claims where x.Type == "preferred_username" select x.Value).FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(usersName) &&
                (from x in proj.Users where x.Name.ToLower().Trim() == usersName.ToLower().Trim() select x).Any())
            {
                return true;
            }

            return false;
        }

        [HttpGet]
        public async Task<IActionResult> GetProject(int id)
        {
            if (!ModelState.IsValid) // SonarQube security
            {
                return View("/");
            }

            var minioEndpoint = _clientHelper.CallAPIWithoutModel<MinioEndpoint>("/api/Project/GetMinioEndPoint");
            var paramlist = new Dictionary<string, string>();
            paramlist.Add("projectId", id.ToString());
            var projectawait = _clientHelper.CallAPIWithoutModel<SubmissionGetProjectModel>(
                "/api/Project/GetProjectUI/", paramlist);

            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            await projectawait;
            //Log.Error("projectawait took ElapsedMilliseconds" + stopwatch.ElapsedMilliseconds);
            await minioEndpoint;
            //Log.Error("minioEndpoint took ElapsedMilliseconds" + stopwatch.ElapsedMilliseconds);
            stopwatch.Stop();
            var project = projectawait.Result;

            var userItems2 = project.UsersNotInProject;
            var treItems2 = project.TresNotInProject;

            var userItems = userItems2
                .Select(p => new SelectListItem
                {
                    Value = p.Id.ToString(),
                    Text = !string.IsNullOrWhiteSpace(p.FullName)
                        ? p.FullName
                        : (!string.IsNullOrWhiteSpace(p.Name) ? p.Name : $"[id:{p.Id}]")
                })
                .ToList();

            var tres = _clientHelper.CallAPIWithoutModel<List<Tre>>("/api/Tre/GetAllTres/").Result;

            // Process TRE names for display
            var treItems = treItems2
                .Select(p => new SelectListItem { Value = p.Id.ToString(), Text = p.Name })
                .ToList();


            ViewBag.UserCanDoSubmissions = IsUserOnProject(project);

            ViewBag.minioendpoint = minioEndpoint.Result.Url;
            ViewBag.URLBucket = _URLSettingsFrontEnd.MinioUrl;

            var projectView = new ProjectUserTre()
            {
                Id = project.Id,
                FormData = project.FormData,
                Name = project.Name,
                Users = project.Users,
                StartDate = project.StartDate,
                EndDate = project.EndDate,
                ProjectDescription = project.ProjectDescription,
                ProjectContact = project.ProjectContact,
                ProjectOwner = project.ProjectOwner,
                Tres = project.Tres,
                SubmissionBucket = project.SubmissionBucket,
                OutputBucket = project.OutputBucket,
                MinioEndpoint = minioEndpoint.Result.Url,
                Submissions = project.Submissions.Where(x => x.HasParent == false).ToList(),
                UserItemList = userItems,
                TreItemList = treItems
            };

            //Log.Error("View(projectView) took ElapsedMilliseconds" + stopwatch.ElapsedMilliseconds);
            return View(projectView);
        }

        public IActionResult SubmissionProjectSqlNew(int id)
        {
          if (!ModelState.IsValid) // SonarQube security
          {
            return View("/");
          }

          var paramlist = new Dictionary<string, string>();
          paramlist.Add("projectId", id.ToString());
          var project = _clientHelper.CallAPIWithoutModel<SubmissionGetProjectModel>(
            "/api/Project/GetProjectUI/", paramlist).Result;


          ViewBag.UserCanDoSubmissions = IsUserOnProject(project);

          var projectView = new ProjectUserTre()
          {
            Id = project.Id,
            Name = project.Name,
            Submissions = project.Submissions.Where(x => x.HasParent == false).ToList(),
          };

          return View(projectView);
        }
        public IActionResult SubmissionProjectSQL(int id)
        {
            if (!ModelState.IsValid) // SonarQube security
            {
                return View("/");
            }

            var paramlist = new Dictionary<string, string>();
            paramlist.Add("projectId", id.ToString());
            var project = _clientHelper.CallAPIWithoutModel<SubmissionGetProjectModel>(
                "/api/Project/GetProjectUI/", paramlist).Result;


            ViewBag.UserCanDoSubmissions = IsUserOnProject(project);

            var projectView = new ProjectUserTre()
            {
                Id = project.Id,
                Name = project.Name,
                Submissions = project.Submissions.Where(x => x.HasParent == false).ToList(),
            };

            return View(projectView);
        }


        public IActionResult SubmissionProjectGraphQL(int id)
        {
            if (!ModelState.IsValid) // SonarQube security
            {
                return View("/");
            }

            var paramlist = new Dictionary<string, string>();
            paramlist.Add("projectId", id.ToString());
            var project = _clientHelper.CallAPIWithoutModel<SubmissionGetProjectModel>(
                "/api/Project/GetProjectUI/", paramlist).Result;

            ViewBag.UserCanDoSubmissions = IsUserOnProject(project);

            var projectView = new ProjectUserTre()
            {
                Id = project.Id,
                Name = project.Name,
                Submissions = project.Submissions.Where(x => x.HasParent == false).ToList()
            };

            return View(projectView);
        }

        public IActionResult SubmissionProjectDemo(int id)
        {
            if (!ModelState.IsValid) // SonarQube security
            {
                return View("/");
            }

            var paramlist = new Dictionary<string, string>();
            paramlist.Add("projectId", id.ToString());
            var project = _clientHelper.CallAPIWithoutModel<SubmissionGetProjectModel>(
                "/api/Project/GetProjectUI/", paramlist).Result;

            ViewBag.UserCanDoSubmissions = IsUserOnProject(project);

            var projectView = new ProjectUserTre()
            {
                Id = project.Id,
                Name = project.Name,
                Submissions = project.Submissions.Where(x => x.HasParent == false).ToList()
            };

            return View(projectView);
        }

        [HttpGet]
        public IActionResult GetAllProjects()
        {
            var projects = _clientHelper.CallAPIWithoutModel<List<Project>>("/api/Project/GetAllProjects/").Result;
            return View(projects);
        }

        [HttpGet]
        [Authorize(Roles = "dare-control-admin")]
        public IActionResult AddUserMembership()
        {
            if (!ModelState.IsValid) // SonarQube security
            {
                return View("/");
            }

            var projmem = GetProjectUserModel();
            return View(projmem);
        }


        private ProjectUser GetProjectUserModel()
        {
            var projs = _clientHelper.CallAPIWithoutModel<List<Project>>("/api/Project/GetAllProjects/").Result;
            var users = _clientHelper.CallAPIWithoutModel<List<User>>("/api/User/GetAllUsers/").Result;

            var projectItems = projs
                .Select(p => new SelectListItem { Value = p.Id.ToString(), Text = p.Name })
                .ToList();

            var userItems = users
                .Select(p => new SelectListItem { Value = p.Id.ToString(), Text = p.Name })
                .ToList();

            var projmem = new ProjectUser()
            {
                ProjectItemList = projectItems,
                UserItemList = userItems
            };
            return projmem;
        }

        [HttpGet]
        [Authorize(Roles = "dare-control-admin")]
        public IActionResult AddTreMembership()
        {
            if (!ModelState.IsValid) // SonarQube security
            {
                return View("/");
            }

            var projmem = GetProjectTreModel();
            return View(projmem);
        }


        private ProjectTre GetProjectTreModel()
        {
            var projs = _clientHelper.CallAPIWithoutModel<List<Project>>("/api/Project/GetAllProjects/").Result;
            var users = _clientHelper.CallAPIWithoutModel<List<Tre>>("/api/Tre/GetAllTres/").Result;

            var projectItems = projs
                .Select(p => new SelectListItem { Value = p.Id.ToString(), Text = p.Name })
                .ToList();

            var treItems = users
                .Select(p => new SelectListItem { Value = p.Id.ToString(), Text = p.Name })
                .ToList();

            var projmem = new ProjectTre()
            {
                ProjectItemList = projectItems,
                TreItemList = treItems
            };
            return projmem;
        }

        [HttpPost]
        [Authorize(Roles = "dare-control-admin")]
        public async Task<IActionResult> AddUserMembership(ProjectUser model)
        {
            if (!ModelState.IsValid) // SonarQube security
            {
                return View("/");
            }

            var result =
                await _clientHelper.CallAPI<ProjectUser, ProjectUser?>("/api/Project/AddUserMembership", model);
            result = GetProjectUserModel();
            return View(result);
        }

        [HttpPost]
        [Authorize(Roles = "dare-control-admin")]
        public async Task<IActionResult> AddTreMembership(ProjectTre model)
        {
            if (!ModelState.IsValid) // SonarQube security
            {
                return View("/");
            }

            var result =
                await _clientHelper.CallAPI<ProjectTre, ProjectTre?>("/api/Project/AddTreMembership",
                    model);
            result = GetProjectTreModel();

            return View(result);
        }


        [HttpGet]
        [Authorize(Roles = "dare-control-admin")]
        public IActionResult SaveProjectForm(int projectId)
        {
            if (!ModelState.IsValid) // SonarQube security
            {
                return View("/");
            }

            var formData = new FormData()
            {
                FormIoUrl = _formIOSettings.ProjectForm,
                FormIoString = @"{""id"":0}",
                Id = projectId
            };

            if (projectId > 0)
            {
                var paramList = new Dictionary<string, string>();
                paramList.Add("projectId", projectId.ToString());
                var project = _clientHelper
                    .CallAPIWithoutModel<Project>("/api/Project/GetProject/", paramList).Result;
                formData.FormIoString = project?.FormData;

                formData.FormIoString = formData.FormIoString?.Replace(@"""id"":0", @"""Id"":" + projectId.ToString(),
                    StringComparison.CurrentCultureIgnoreCase);
            }

            return View(formData);
        }


        [HttpPost]
        [Authorize(Roles = "dare-control-admin")]
        public async Task<IActionResult> ProjectFormSubmission([FromBody] object arg, int id)
        {
            if (!ModelState.IsValid) // SonarQube security
            {
                return View("/");
            }

            var str = arg?.ToString();

            if (!string.IsNullOrEmpty(str))
            {
                var data = System.Text.Json.JsonSerializer.Deserialize<FormData>(str);
                data.FormIoString = str;

                var result = await _clientHelper.CallAPI<FormData, Project?>("/api/Project/SaveProject", data);

                if (result.Id == 0)
                {
                    TempData["error"] = result.FormData;
                    return BadRequest();
                }

                TempData["success"] = "Project Save Successfully";

                return Ok(result);
            }

            return BadRequest();
        }

        [Authorize(Roles = "dare-control-admin")]
        public async Task<IActionResult> RemoveUserFromProject(int projectId, int userId)
        {
            if (!ModelState.IsValid) // SonarQube security
            {
                return View("/");
            }

            var model = new ProjectUser()
            {
                ProjectId = projectId,
                UserId = userId
            };
            var result =
                await _clientHelper.CallAPI<ProjectUser, ProjectUser?>("/api/Project/RemoveUserMembership", model);
            TempData["success"] = "User Remove Successfully";
            return RedirectToAction("GetProject", new { id = projectId });
        }

        [HttpGet]
        [Authorize(Roles = "dare-control-admin")]
        public async Task<IActionResult> RemoveTreFromProject(int projectId, int treId)
        {
            if (!ModelState.IsValid) // SonarQube security
            {
                return View("/");
            }

            var model = new ProjectTre()
            {
                ProjectId = projectId,
                TreId = treId
            };
            var result =
                await _clientHelper.CallAPI<ProjectTre, ProjectTre?>("/api/Project/RemoveTreMembership", model);
            TempData["success"] = "Tre Remove Successfully";
            return RedirectToAction("GetProject", new { id = projectId });
        }

        [HttpPost]
        [Authorize(Roles = "dare-control-admin")]
        public async Task<IActionResult> AddUserList(string ProjectId, string ItemList)
        {
            string[] arr = ItemList.Split(',');
            List<string> userList = new List<string>();
            bool addedUser = false;
            foreach (string s in arr)
            {
                var model = new ProjectUser()
                {
                    ProjectId = Int32.Parse(ProjectId),
                    UserId = Int32.Parse(s)
                };
                var user =
                    await _clientHelper.CallAPI<ProjectUser, ProjectUser?>("/api/Project/CheckUserExists", model);
                if (user.UserId == 0)
                {
                    var paramList = new Dictionary<string, string>();
                    paramList.Add("userId", s.ToString());
                    var userInfo = _clientHelper.CallAPIWithoutModel<User?>("/api/User/GetUser/", paramList)
                        .Result;
                    userList.Add(userInfo.Name);
                }
                else
                {
                    var response =
                        await _clientHelper.CallAPI<ProjectUser, ProjectUser?>("/api/Project/AddUserMembership", model);
                    addedUser = true;
                }
            }

            if (userList.Count > 0)
            {
                var listOfNoneExistingUser = string.Join(", ", userList);
                TempData["error"] = listOfNoneExistingUser + "are not exist in keycloak. Need to Register";
            }

            if (addedUser)
            {
                TempData["success"] = "User Added Successfully";
            }

            return Ok();
        }

        [HttpPost]
        [Authorize(Roles = "dare-control-admin")]
        public async Task<IActionResult> AddTreList(string ProjectId, string ItemList)
        {
            string[] arr = ItemList.Split(',');
            foreach (string s in arr)
            {
                var model = new ProjectTre()
                {
                    ProjectId = Int32.Parse(ProjectId),
                    TreId = Int32.Parse(s)
                };
                var result =
                    await _clientHelper.CallAPI<ProjectTre, ProjectTre?>("/api/Project/AddTreMembership",
                        model);
            }

            TempData["success"] = "Tre Added Successfully";
            return RedirectToAction("GetProject", new { id = ProjectId });
        }

        [HttpGet]
        [Authorize(Roles = "dare-control-admin")]
        public void IsUserOnProject(int projectId, int userId)
        {
            if (!ModelState.IsValid) // SonarQube security
            {
            }

            var model = new ProjectUser()
            {
                ProjectId = projectId,
                UserId = userId
            };
            var result = _clientHelper.CallAPI<ProjectUser, ProjectUser?>("api/Project/IsUserOnProject", model);
        }

        public string ConvertIFormFileToJson(IFormFile formFile)
        {
            if (!ModelState.IsValid) // SonarQube security
            {
                return null;
            }

            if (formFile == null)
                return null;


            using (var stream = new MemoryStream())
            {
                formFile.CopyTo(stream);
                var bytes = stream.ToArray();

                var base64String = Convert.ToBase64String(bytes);

                var fileData = new IFileData
                {
                    FileName = formFile.FileName,
                    ContentType = formFile.ContentType,
                    Content = base64String
                };

                using (var memoryStream = new MemoryStream())
                {
                    System.Text.Json.JsonSerializer.SerializeAsync(memoryStream, fileData).Wait();
                    return System.Text.Encoding.UTF8.GetString(memoryStream.ToArray());
                }
            }
        }
    }
}
