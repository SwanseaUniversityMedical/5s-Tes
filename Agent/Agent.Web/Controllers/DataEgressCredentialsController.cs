using Agent.Web.Services;
using FiveSafesTes.Core.Models;
using FiveSafesTes.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Agent.Web.Controllers
{
    [Authorize(Roles = "dare-tre-admin")]
    public class DataEgressCredentialsController : Controller
    {
        private readonly ITREClientHelper _clientHelper;
        public DataEgressCredentialsController(ITREClientHelper client)
        {
            _clientHelper = client;
        }



        [HttpGet]

        public async Task<IActionResult> UpdateCredentialsAsync()
        {
            ViewBag.IsEgressConfigured = await ControllerHelpers.AreEgressCredentialsConfigured(_clientHelper);
            return View(await ControllerHelpers.CheckCredentialsAreValid("DataEgressCredentials", _clientHelper));
        }



        [HttpPost]

        public async Task<IActionResult> UpdateCredentials(KeycloakCredentials credentials)
        {
            if (!ModelState.IsValid) // SonarQube security
            {
                return View(credentials);
            }
            credentials = await ControllerHelpers.UpdateCredentials("DataEgressCredentials", _clientHelper, ModelState,
                    credentials);
            ViewBag.IsEgressConfigured = await ControllerHelpers.AreEgressCredentialsConfigured(_clientHelper);

            if (credentials.Valid)
            {
                return RedirectToAction("Index", "Home");
            }

            return View(credentials);
        }

    }
}
