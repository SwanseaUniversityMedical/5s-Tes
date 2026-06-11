using FiveSafesTes.Core.Models;
using FiveSafesTes.Core.Models.APISimpleTypeReturns;
using FiveSafesTes.Core.Models.ViewModels;
using FiveSafesTes.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Agent.Web.Controllers;

[Authorize(Roles = "dare-tre-admin")]
public class SubmissionConfigController : Controller
{
    private readonly ITREClientHelper _clientHelper;

    public SubmissionConfigController (ITREClientHelper client)
    {
        _clientHelper = client;
    }

    public IActionResult Index(JsonConfigUploadResponse response)
    {
        var data = _clientHelper.CallAPIWithoutModel<List<HealthCheckStatus>>("/api/Status/GetHealthCheckData").Result;
        if (data != null) {
          response.healthCheckStatus = data;
        }
    return View(response);
    }

    public async Task<IActionResult> UploadJsonConfig(IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            return RedirectToAction("Index", new JsonConfigUploadResponse() { Success = false, Message = "No file uploaded!"});
        }

        JsonConfigUploadResponse? response = await _clientHelper.CallAPIToSendFile<JsonConfigUploadResponse>("/api/Onboarding/UploadJsonConfig", "file", file);

        return RedirectToAction("Index", response);
    }

    public async Task<IActionResult> ReviveHangfireJobs()
    {
        await _clientHelper.CallAPIWithoutModel<BoolReturn>("/api/Onboarding/ReviveHangfireJobs", httpMethod: HttpMethod.Post);

        return Ok();
    }
}
