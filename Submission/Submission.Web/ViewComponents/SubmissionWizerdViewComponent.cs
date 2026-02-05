using FiveSafesTes.Core.Models;
using FiveSafesTes.Core.Models.ViewModels;
using FiveSafesTes.Core.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Submission.Web.ViewComponents
{
    public class SubmissionWizerdViewComponent : ViewComponent
    {
        private readonly IDareClientHelper _clientHelper;
        public SubmissionWizerdViewComponent(IDareClientHelper client)
        {
            _clientHelper = client;
        }
        public async Task<IViewComponentResult> InvokeAsync(int projectId)
        {
            var paramlist = new Dictionary<string, string>();
            paramlist.Add("projectId", projectId.ToString());

            var project = _clientHelper.CallAPIWithoutModel<Project?>(
                   "/api/Project/GetProject/", paramlist).Result;

            //var projectawait = _clientHelper.CallAPIWithoutModel<SubmissionGetProjectModel>(
            //"/api/Project/GetProjectUI/", paramlist);

            var SelectTresOptions = project.Tres.Select(x => new { Name = x.Name, LastHeartBeatReceived = x.LastHeartBeatReceived }).ToList();
            
            List<TreInfo> treInfoList = new List<TreInfo>();
            foreach (var param in SelectTresOptions)
            {
                TimeSpan timeSinceLastUpdate = DateTime.Now - param.LastHeartBeatReceived;
                var isOnline = false;
                if(timeSinceLastUpdate.TotalMinutes<30)
                    isOnline = true;

                var treInfo = new TreInfo()
                {
                    Name = param.Name,
                    IsSelected = false,
                    IsOnline = isOnline
                };
                treInfoList.Add(treInfo);
            }

            var userItems2 = project.Users;
            var treItems2 = project.Tres;

            var userItems = userItems2
                    .Select(p => new SelectListItem { Value = p.Id.ToString(), Text = p.FullName != "" ? p.FullName : p.Name })
                    .ToList();
            var treItems = treItems2
                .Select(p => new SelectListItem { Value = p.Id.ToString(), Text = p.Name })
                .ToList();


            var model = new SubmissionWizard()
            {
                ProjectId = project.Id,
                ProjectName = project.Name,
                SelectTresOptions = project.Tres.Select(x => x.Name).ToList(),
                TreRadios = treInfoList,
                Submissions = project.Submissions.ToList(),
                UserItemList = userItems,
                TreItemList = treItems

            };
            return View(model);
        }
    }
}
