using FiveSafesTes.Core.Models;
using FiveSafesTes.Core.Models.ViewModels;
using FiveSafesTes.Core.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Submission.Web.ViewComponents
{
    public class SubmissionWizardNewViewComponent : ViewComponent
    {
        private readonly IDareClientHelper _clientHelper;
        public SubmissionWizardNewViewComponent(IDareClientHelper client)
        {
            _clientHelper = client;
        }
        public async Task<IViewComponentResult> InvokeAsync(int projectId)
        {
            var paramlist = new Dictionary<string, string>();
            paramlist.Add("projectId", projectId.ToString());
            var project = _clientHelper.CallAPIWithoutModel<Project?>(
                "/api/Project/GetProject/", paramlist).Result;
            var SelectTresOptions = project.Tres.ToList();
            List<TreInfo> treInfoList = new List<TreInfo>();
            foreach (var param in SelectTresOptions)
            {
                var isOnline = param.IsOnline();

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

            var model = new AddiSubmissionWizard()
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
