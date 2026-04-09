using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using FiveSafesTes.Core.Models;
using FiveSafesTes.Core.Models.ViewModels;
using FiveSafesTes.Core.Services;
using Serilog;
using Submission.Web.Models;

namespace Submission.Web.Controllers
{

    [AllowAnonymous]
    public class HomeController : Controller
    {

        private readonly IDareClientHelper _clientHelper;
        private readonly IConfiguration _configuration;
        private readonly UIName _UIName;


        public HomeController(IDareClientHelper client, IConfiguration configuration, UIName uIName)
        {
            _clientHelper = client;
            _configuration = configuration;
            _UIName = uIName;
        }

        public async Task<IActionResult> Index()
        {
            ViewBag.getAllProj = 0;
            ViewBag.getAllSubs = 0;
            ViewBag.getAllUsers = 0;
            ViewBag.getAllTres = 0;
            ViewBag.UIName = _UIName.Name;


            try
            {
                var counts = await _clientHelper.CallAPIWithoutModel<DashboardCounts>("/api/Metrics/GetPublicCounts");
                if (counts != null)
                {
                    ViewBag.getAllProj = counts.ProjectCount;
                    ViewBag.getAllSubs = counts.SubmissionCount;
                    ViewBag.getAllUsers = counts.UserCount;
                    ViewBag.getAllTres = counts.TreCount;
                }
            }
            catch (Exception e)
            {
                Log.Warning(e, "{Function} Unable to call api. Might just be initialisation issue");

            }


            foreach (var Claim in User.Claims)
            {
                Log.Debug($"User has Claim {Claim.ToString()}");
            }


            return View();
        }

        [HttpPost]
        public async Task<IActionResult> SearchView(string searchString)
        {
            List<Project> results = await SearchData(searchString);

            {
                if (results != null)
                {
                    ViewBag.SearchResults = results;
                    ViewBag.SearchString = searchString;
                }
                else
                {
                    //results = new List<Project>();
                    ViewBag.SearchResults = "No search results found.";
                    //ViewBag.SearchString = "No Results found";
                }
                return View();
            }
        }

        //private helpers
        private async Task<List<Project>> SearchData(string searchString)
        {
            try
            {
                var paramlist = new Dictionary<string, string>();
                paramlist.Add("searchString", searchString);
                var results = await _clientHelper.CallAPIWithoutModel<List<Project>>("/api/Project/GetSearchData/", paramlist);
                if (results == null)
                {
                    return new List<Project>();
                }

                return results;
            }
            catch (Exception ex)
            {
                Log.Error(ex.ToString());
                return null;
            }
        }
       

        [Authorize]
        public async Task<IActionResult> LoggedInUser()
        {
            if(User.Identity.IsAuthenticated == false) {
                return RedirectToAction("Index", "Home");
            }

            var countsTask = _clientHelper.CallAPIWithoutModel<DashboardCounts>("/api/Metrics/GetCurrentUserCounts");
            var userProjectsTask = _clientHelper.CallAPIWithoutModel<List<Project>>("/api/Project/GetProjectsForCurrentUser");
            var userSubmissionsTask = _clientHelper.CallAPIWithoutModel<List<FiveSafesTes.Core.Models.Submission>>("/api/Submission/GetSubmissionsForCurrentUser");

            await Task.WhenAll(countsTask, userProjectsTask, userSubmissionsTask);

            var counts = countsTask.Result;
            var userProjects = userProjectsTask.Result ?? new List<Project>();
            var userSubmissions = userSubmissionsTask.Result ?? new List<FiveSafesTes.Core.Models.Submission>();

            if (counts != null)
            {
                ViewBag.getAllProj = counts.ProjectCount;
                ViewBag.getAllSubs = counts.SubmissionCount;
                ViewBag.getAllUsers = counts.UserCount;
                ViewBag.getAllTres = counts.TreCount;
                ViewBag.userOnProjectCount = counts.UserOnProjectCount;
                ViewBag.userWroteSubCount = counts.UserWroteSubmissionCount;
            }
            else
            {
                ViewBag.userOnProjectCount = 0;
                ViewBag.userWroteSubCount = 0;
            }

            var userModel = new User
            {
                Name = User.Identity.Name,

                Projects = userProjects,

                Submissions = userSubmissions,
        };
            
            return View(userModel);
        }

      


        public IActionResult TermsAndConditions()
        {
            return View();
        }
        public IActionResult PrivacyPolicy()
        {
            return View();
        }
    }

}

