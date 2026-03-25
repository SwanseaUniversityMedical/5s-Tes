using System.Text.Json;
using FiveSafesTes.Core.Models;
using FiveSafesTes.Core.Models.Tes;
using FiveSafesTes.Core.Models.ViewModels;
using FiveSafesTes.Core.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Submission.Web.ViewComponents;

public class SubmissionWizardDemoViewComponent : ViewComponent
{
    private readonly IDareClientHelper _clientHelper;

    public SubmissionWizardDemoViewComponent(IDareClientHelper clientHelper)
    {
        _clientHelper = clientHelper;
    }

    public async Task<IViewComponentResult> InvokeAsync(int projectId)
    {
        var parameters = new Dictionary<string, string>();
        parameters.Add("projectId", projectId.ToString());
        var project = _clientHelper.CallAPIWithoutModel<Project?>(
                          "/api/Project/GetProject/", parameters).Result ??
                      throw new NullReferenceException($"Project with id: {projectId} not found");
        var selectTresOptions = project.Tres.ToList();
        List<TreInfo> treInfoList = new List<TreInfo>();
        foreach (var param in selectTresOptions)
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

        var users = project.Users;
        var tres = project.Tres;
        var userItems = users
            .Select(p => new SelectListItem { Value = p.Id.ToString(), Text = p.FullName != "" ? p.FullName : p.Name })
            .ToList();
        var treItems = tres
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
            TreItemList = treItems,
            JsonData = JsonSerializer.Serialize(new TesTask()
            {
                State = 0,
                Name = "Hello World",
                Inputs = new List<TesInput>(),
                Outputs = new List<TesOutput>()
                {
                    new TesOutput()
                    {
                        Name = "Hello World",
                        Description = "Stdout file",
                        Url = "outputbucket",
                        Path = "/outputs",
                        Type = TesFileType.DIRECTORYEnum
                    }
                },
                Executors = new List<TesExecutor>()
                {
                    new TesExecutor()
                    {
                        Image = "ubuntu",
                        Command = new List<string>() { "echo", "Hello World" },
                        Workdir = "/outputs",
                        Stdout = "/outputs/stdout",
                    }
                }
            }, new JsonSerializerOptions { WriteIndented = true })
        };
        return View(model);
    }
}
