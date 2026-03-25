using FiveSafesTes.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using FiveSafesTes.Core.Models.Settings;
using FiveSafesTes.Core.Models.ViewModels;
using FiveSafesTes.Core.Services;
using Microsoft.AspNetCore.Mvc.Rendering;


namespace Submission.Web.Controllers
{
    [Authorize]
    public class UserController : Controller
    {
        private readonly IDareClientHelper _clientHelper;
        
        private readonly FormIOSettings _formIOSettings;

        public UserController(IDareClientHelper client, FormIOSettings formIo)
        {
            _clientHelper = client;
            
            _formIOSettings = formIo;
         
        }

        [HttpGet]
        [Authorize(Roles = "dare-control-admin")]
        public IActionResult SaveUserForm(int userId)
        {
            if (!ModelState.IsValid) // SonarQube security
            {
                return View("/");
            }

            var formData = new FormData()
            {
                FormIoUrl = _formIOSettings.UserForm,
                FormIoString = @"{""id"":0}",
                Id = userId
            };
            
            if (userId > 0)
            {
                var paramList = new Dictionary<string, string>();
                paramList.Add("userId", userId.ToString());
                var user = _clientHelper.CallAPIWithoutModel<User?>("/api/User/GetUser/", paramList).Result;
                formData.FormIoString = user?.FormData;
                formData.FormIoString = formData.FormIoString?.Replace(@"""id"":0", @"""Id"":"+userId.ToString(), StringComparison.CurrentCultureIgnoreCase);
            }

            return View(formData);
        }

        [HttpGet]
        public IActionResult GetAllUsers()
        {

            var result = _clientHelper.CallAPIWithoutModel<List<User>>("/api/User/GetAllUsers/").Result;

            return View(result);
        }
        
        
        [HttpGet]
        public IActionResult GetUser(int id)
        {
            if (!ModelState.IsValid) // SonarQube security
            {
                return View("/");
            }

            var projects = _clientHelper.CallAPIWithoutModel<List<Project>>("/api/Project/GetAllProjects/").Result;            
            var paramlist = new Dictionary<string, string>();
            paramlist.Add("userId", id.ToString());
            var result = _clientHelper.CallAPIWithoutModel<User?>(
                "/api/User/GetUser/", paramlist).Result;

            var projectItems2 = projects.Where(p => !result.Projects.Select(x => x.Id).Contains(p.Id)).ToList();          

            var projectItems = projectItems2
                .Select(p => new SelectListItem { Value = p.Id.ToString(), Text = p.Name })
                .ToList();
           

            ViewBag.ProjectItems = projectItems;

            return View(result);
        }

        [HttpPost]
        [Authorize(Roles = "dare-control-admin")]
        public async Task<IActionResult> UserEditFormSubmission([FromBody] object arg, int id)
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

                var result = await _clientHelper.CallAPI<FormData, User>("/api/User/SaveUser", data);

                if (result.Id == 0)
                {
                    TempData["error"] = "";
                    return BadRequest();
                }
                TempData["success"] = "User Save Successfully";
                return Ok(result);
            }
            return BadRequest();
        }

        [HttpGet]
        [Authorize(Roles = "dare-control-admin")]
        public IActionResult AddProjectMembership()
        {

            var projmem = GetProjectUserModel();
            return View(projmem);
        }
     
        [HttpPost]
        [Authorize(Roles = "dare-control-admin")]
        public async Task<IActionResult> AddProjectMembership(ProjectUser model)
        {
            if (!ModelState.IsValid) // SonarQube security
            {
                return View("/");
            }

            var result =
                await _clientHelper.CallAPI<ProjectUser, ProjectUser?>("/api/User/AddProjectMembership", model);
            result = GetProjectUserModel();


            return View(result);
        }

        [HttpGet]
        [Authorize(Roles = "dare-control-admin")]
        public async Task<IActionResult> RemoveProjectFromUser(int userId, int projectId)
        {
            if (!ModelState.IsValid) // SonarQube security
            {
                return View("/");
            }

            var model = new ProjectUser()
            {
                UserId = userId,
                ProjectId = projectId               
            };

            var result = await _clientHelper.CallAPI<ProjectUser, ProjectUser?>("/api/User/RemoveProjectMembership", model);
            TempData["success"] = "Project Remove Successfully";
            return RedirectToAction("GetUser", new { id = userId });
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

        [HttpPost]
        [Authorize(Roles = "dare-control-admin")]
        public async Task<IActionResult> AddProjectList(string Id, string ItemList)
        {
            string[] arr = ItemList.Split(',');
            foreach (string s in arr)
            {
                var model = new ProjectUser()
                {
                    UserId = Int32.Parse(Id),
                    ProjectId = Int32.Parse(s)
                };
                var user =
                await _clientHelper.CallAPI<ProjectUser, ProjectUser?>("/api/Project/CheckUserExists", model);
                if (user.UserId == 0)
                {
                    TempData["error"] = "User not found. Need to Register";
                    return Ok();
                }
                var result =
                await _clientHelper.CallAPI<ProjectUser, ProjectUser?>("/api/User/AddProjectMembership", model);
            }
            TempData["success"] = "Project Added Successfully";
            //return RedirectToAction("GetUser", new { id = Id });
            return Ok();
        }

    }
}
