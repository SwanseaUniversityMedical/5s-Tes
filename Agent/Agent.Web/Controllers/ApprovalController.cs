using FiveSafesTes.Core.Models;
using FiveSafesTes.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Agent.Web.Controllers
{
    [Authorize(Roles = "dare-tre-admin")]
    public class ApprovalController : Controller
    {

        private readonly ITREClientHelper _treclientHelper;
        public ApprovalController(ITREClientHelper treClient)
        {

            _treclientHelper = treClient;
        }
        
     
        [HttpGet]
        public IActionResult GetAllProjects(bool showOnlyUnprocessed)
        {
            if (!ModelState.IsValid) // SonarQube security
            {
                return View("/");
            }

            var paramlist = new Dictionary<string, string>
            {
               { "showOnlyUnprocessed", showOnlyUnprocessed.ToString() }
            };
            var projects = _treclientHelper.CallAPIWithoutModel<List<TreProject>>("/api/Approval/GetAllTreProjects/", paramlist).Result;

            return View(projects);
        }

        [HttpGet]
        public IActionResult EditMemberships(int projectId, bool showOnlyUnprocessed)
        {
            if (!ModelState.IsValid) // SonarQube security
            {
                return View("/");
            }

            var paramlist = new Dictionary<string, string>
            {
                { "projectId", projectId.ToString() },
                { "showOnlyUnprocessed", showOnlyUnprocessed.ToString() }
            };
            var members = _treclientHelper.CallAPIWithoutModel<List<TreMembershipDecision>>(
                "/api/Approval/GetMemberships/", paramlist).Result;

            return View(members);
        }


        [HttpPost]
        public async Task<IActionResult> EditMemberships(List<TreMembershipDecision> model)
        {
            if (!ModelState.IsValid) // SonarQube security
            {
                return View("/");
            }

            var result =
                await _treclientHelper.CallAPI<List<TreMembershipDecision>, List<TreMembershipDecision>>("/api/Approval/UpdateMembershipDecisions", model);

            return View(result);
        }

        [HttpPost]
        public async Task<IActionResult> EditProject(TreProject model)
        {
            if (!ModelState.IsValid) // SonarQube security
            {
                return View("/");
            }

            var result =
                await _treclientHelper.CallAPI<List<TreProject>, List<TreProject>>("/api/Approval/UpdateProjects", new List<TreProject>(){ model});

            return View(result.First());
        }
        
        [HttpGet]
        public IActionResult EditProject(int? projectId)
        {
            if (!ModelState.IsValid) // SonarQube security
            {
                return View("/");
            }

            var paramlist = new Dictionary<string, string>();
            paramlist.Add("projectId", projectId.ToString());
            var project = _treclientHelper.CallAPIWithoutModel<TreProject>(
                "/api/Approval/GetTreProject/", paramlist).Result;

            return View(project);
        }

    }

}
