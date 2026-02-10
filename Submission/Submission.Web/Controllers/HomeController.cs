using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using FiveSafesTes.Core.Models;
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

        public IActionResult Index()
        {
            ViewBag.getAllProj = 0;
            ViewBag.getAllSubs = 0;
            ViewBag.getAllUsers = 0;
            ViewBag.getAllTres = 0;
            ViewBag.UIName = _UIName.Name;


            try
            {
                var getAllProj = _clientHelper.CallAPIWithoutModel<List<Project>>("/api/Project/GetAllProjects").Result;
                ViewBag.getAllProj = getAllProj.Count;


                var getAllSubs = _clientHelper
                    .CallAPIWithoutModel<List<FiveSafesTes.Core.Models.Submission>>("/api/Submission/GetAllSubmissions").Result
                    .Where(x => x.Parent == null).ToList();
                ViewBag.getAllSubs = getAllSubs.Count;

                var getAllUsers = _clientHelper.CallAPIWithoutModel<List<User>>("/api/User/GetAllUsers").Result;
                ViewBag.getAllUsers = getAllUsers.Count;

                var getAllTres = _clientHelper.CallAPIWithoutModel<List<Tre>>("/api/Tre/GetAllTres").Result;
                ViewBag.getAllTres = getAllTres.Count;
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
        public IActionResult SearchView(string searchString)
        {
            List<Project> results = SearchData(searchString);

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
        private List<Project> SearchData(string searchString)
        {
            try
            {
                var paramlist = new Dictionary<string, string>();
                paramlist.Add("searchString", searchString);
                var results = _clientHelper.CallAPIWithoutModel<List<Project>>("/api/Project/GetSearchData/", paramlist).Result.ToList();
                return results;
            }
            catch (Exception ex)
            {
                Log.Error(ex.ToString());
                return null;
            }
        }
       

        [Authorize]
        public IActionResult LoggedInUser()
        {
            if(User.Identity.IsAuthenticated == false) {
                return RedirectToAction("Index", "Home");
            }

            // This var is always in lower case/case-insensitive because we are getting it from KeyCloak 
            var preferedUsername = (from x in User.Claims where x.Type == "preferred_username" select x.Value).First();
            
            var getAllProj = _clientHelper.CallAPIWithoutModel<List<Project>>("/api/Project/GetAllProjects").Result;
            ViewBag.getAllProj = getAllProj;

            var getAllSubs = _clientHelper.CallAPIWithoutModel<List<FiveSafesTes.Core.Models.Submission>>("/api/Submission/GetAllSubmissions").Result.Where(x => x.Parent == null).ToList();
            ViewBag.getAllSubs = getAllSubs.Count;

            var getAllUsers = _clientHelper.CallAPIWithoutModel<List<User>>("/api/User/GetAllUsers").Result;
            ViewBag.getAllUsers = getAllUsers.Count;

            var getAllTres = _clientHelper.CallAPIWithoutModel<List<Tre>>("/api/Tre/GetAllTres").Result;
            ViewBag.getAllTres = getAllTres.Count;

            var userOnProjList = new List<User>();
            var userOnProjListProj = new List<Project>();
            var projectList = _clientHelper.CallAPIWithoutModel<List<Project>>("/api/Project/GetAllProjects").Result.ToList();
            foreach (var proj in projectList)
            {
                foreach (var user in proj.Users)
                {
                    // Making sure that the username getting from the DB is lowered/case-insensitive as well as username from KeyCloak
                    var loweredUserName = user.Name.ToLower();
                    if (loweredUserName == preferedUsername)
                    {
                        userOnProjListProj.Add(proj);

                        userOnProjList.Add(user);
                    }
                }
            }
            var userOnProjectsCount = userOnProjList.ToList().Count;
            ViewBag.userOnProjectCount = userOnProjectsCount;

            var userWroteSubList = new List<User>();
            var userWroteSubListSub = new List<FiveSafesTes.Core.Models.Submission>();
            var subList = getAllSubs;
            foreach (var sub in subList)
            {

                    if (sub.SubmittedBy.Name == preferedUsername)
                {
                    userWroteSubListSub.Add(sub);
                    userWroteSubList.Add(sub.SubmittedBy);
                
                }

            }
            var userWroteSubCount = userWroteSubList.ToList().Count;
            var distintProj = userOnProjListProj.Distinct();
            var distinctSub = userWroteSubListSub.Distinct();
            ViewBag.userWroteSubCount = userWroteSubCount;

            var userModel = new User
            {
                Name = User.Identity.Name,

                Projects = distintProj.ToList(),

                Submissions = distinctSub.ToList(),
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

