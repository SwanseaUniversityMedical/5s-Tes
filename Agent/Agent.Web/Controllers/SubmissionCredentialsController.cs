using Agent.Web.Services;
using FiveSafesTes.Core.Models;
using FiveSafesTes.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Agent.Web.Controllers
{
    [Authorize(Roles = "dare-tre-admin")]
    public class SubmissionCredentialsController : Controller
    {
        private readonly ITREClientHelper _clientHelper;
        public SubmissionCredentialsController(ITREClientHelper client)
        {
            _clientHelper = client;
        }



        [HttpGet]

        public async Task<IActionResult> UpdateCredentialsAsync()
        {
            KeycloakCredentials creds = await ControllerHelpers.CheckCredentialsAreValid("SubmissionCredentials", _clientHelper);
            bool configUploaded = await ControllerHelpers.IsConfigurationUploaded(_clientHelper);
            bool treSynced = await ControllerHelpers.IsTRESynced(_clientHelper);

            return View(new SubmissionKeycloakCredentialDTO() 
            { 
                Creds = creds,
                IsSynced = treSynced,
                IsConfigurationUploaded = configUploaded
            });
            
        }

        [HttpPost]
        
        public async Task<IActionResult> UpdateCredentials(SubmissionKeycloakCredentialDTO credentials) {

            if (!ModelState.IsValid) // SonarQube security
            {
                return View(credentials);
            }

            credentials.Creds = await ControllerHelpers.UpdateCredentials("SubmissionCredentials", _clientHelper, ModelState, credentials.Creds);
            credentials.IsConfigurationUploaded = await ControllerHelpers.IsConfigurationUploaded(_clientHelper);
            credentials.IsSynced = await ControllerHelpers.IsTRESynced(_clientHelper);

            if (credentials.Creds.Valid)
            {
                return RedirectToAction("Index", "Home");
            }
            else
            {
                return View(credentials);
            }
            
        }

    }
}
