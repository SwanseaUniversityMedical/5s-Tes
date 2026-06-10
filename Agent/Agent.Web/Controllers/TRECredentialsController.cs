using Agent.Web.Services;
using FiveSafesTes.Core.Models;
using FiveSafesTes.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Agent.Web.Controllers
{
    [Authorize(Roles = "dare-tre-admin")]
    public class TRECredentialsController : Controller
    {
        private readonly ITREClientHelper _clientHelper;
        public TRECredentialsController(ITREClientHelper client)
        {
            _clientHelper = client;
        }
        [HttpGet]
        public async Task<IActionResult> UpdateCredentialsAsync()
        {
            ViewBag.IsSubmissionConfigured = await ControllerHelpers.IsConfigurationUploaded(_clientHelper);
            return View(await ControllerHelpers.CheckCredentialsAreValid("TRECredentials", _clientHelper));
        }
        [HttpPost]

        public async Task<IActionResult> UpdateCredentials(KeycloakCredentials credentials)
        {
            if (!ModelState.IsValid) // SonarQube security
            {
                return View(new KeycloakCredentials());
            }
            credentials = await ControllerHelpers.UpdateCredentials("TRECredentials", _clientHelper, ModelState,
                    credentials);
            ViewBag.IsSubmissionConfigured = await ControllerHelpers.IsConfigurationUploaded(_clientHelper);

            if (credentials.Valid)
            {
                return RedirectToAction("Index", "Home");
            }

            return View(credentials);
        }
    }
}
