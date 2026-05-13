using FiveSafesTes.Core.Models.APISimpleTypeReturns;
using FiveSafesTes.Core.Models.ViewModels;
using FiveSafesTes.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Agent.Web.Controllers;

[Authorize(Roles = "dare-tre-admin")]
public class DeploymentController : Controller
{
    private readonly ITREClientHelper _clientHelper;

    public DeploymentController (ITREClientHelper client)
    {
        _clientHelper = client;
    }

    public IActionResult Index(JsonConfigUploadResponse response)
    {
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
}
