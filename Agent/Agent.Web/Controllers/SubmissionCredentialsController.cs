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

            // First we need to make sure we remove any existing submission credentials to avoid conflicting information.
            await ControllerHelpers.WipeVaultCredentials(_clientHelper);

            // Then we can update the credentials.
            credentials.Creds = await ControllerHelpers.UpdateCredentials("SubmissionCredentials", _clientHelper, ModelState, credentials.Creds);

            // Ensure that the details are valid
            if (credentials.Creds.Error || !credentials.Creds.Valid)
            {
                // Don't keep invalid credentials
                await ControllerHelpers.WipeVaultCredentials(_clientHelper);
            }
            else
            {
                credentials.IsConfigurationUploaded = await ControllerHelpers.IsConfigurationUploaded(_clientHelper);
                credentials.IsSynced = await ControllerHelpers.IsTRESynced(_clientHelper);

                // Ensure that the details belong to a user with a TRE
                if (credentials.IsSynced && await ControllerHelpers.IsUserAssignedTRE(_clientHelper) == false)
                {
                    credentials.Creds.Valid = false;
                    credentials.Creds.ErrorMessage = "User " + credentials.Creds.UserName + " doesn't have a TRE";
                    // Don't keep invalid credentials
                    await ControllerHelpers.WipeVaultCredentials(_clientHelper);
                }
            }

            return View(credentials);
        }

    }
}
