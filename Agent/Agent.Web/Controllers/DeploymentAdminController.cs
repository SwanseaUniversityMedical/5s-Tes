using FiveSafesTes.Core.Models;
using Agent.Web.Services;
using FiveSafesTes.Core.Models.APISimpleTypeReturns;
using FiveSafesTes.Core.Models.ViewModels;
using FiveSafesTes.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Agent.Web.Controllers;

[Authorize(Roles = "dare-tre-admin")]
public class DeploymentAdminController : Controller
{
    private readonly ITREClientHelper _clientHelper;

    public DeploymentAdminController (ITREClientHelper client)
    {
        _clientHelper = client;
    }

    public async Task<IActionResult> Index(JsonConfigUploadResponse response)
    {
        var data = _clientHelper.CallAPIWithoutModel<List<HealthCheckStatus>>("/api/HealthCheck/GetHealthCheckData").Result;
        if (data != null) 
        {
          response.HealthCheckStatus = data;
        }

        response.SyncJobExists = await ControllerHelpers.IsSyncJobCreated(_clientHelper);
        response.IsUploaded = await ControllerHelpers.IsConfigurationUploaded(_clientHelper);
        response.IsSynced = await ControllerHelpers.IsTRESynced(_clientHelper);
        ViewData["IsAirlockConfigured"] = (await _clientHelper.CallAPIWithoutModel<BoolReturn>("/api/Onboarding/IsAirlockConfigured")).Result;
        return View(response);
    }

    [HttpPost]
    public async Task<IActionResult> UploadAirlockConfig(IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            TempData["AirlockError"] = "No file uploaded!";
            return RedirectToAction("Index");
        }

        JsonConfigUploadResponse? response = await _clientHelper.CallAPIToSendFile<JsonConfigUploadResponse>("/api/Onboarding/UploadAirlockConfig", "file", file);

        if (response?.Success == true)
        {
            TempData["AirlockSuccess"] = response.Message;
        }
        else
        {
            TempData["AirlockError"] = response?.Message ?? "Uploading the Airlock config failed.";
        }

        return RedirectToAction("Index");
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
