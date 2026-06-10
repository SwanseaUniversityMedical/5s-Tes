using System.Net;
using FiveSafesTes.Core.Models;
using FiveSafesTes.Core.Models.APISimpleTypeReturns;
using FiveSafesTes.Core.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Agent.Web.Services
{
    public class ControllerHelpers
    {
        public static async Task<KeycloakCredentials> CheckCredentialsAreValid(string controller, ITREClientHelper _clientHelper)
        {
            var valid = await _clientHelper.CallAPIWithoutModel<BoolReturn>("/api/" + controller+ "/CheckCredentialsAreValid");


            return new KeycloakCredentials() { Valid = valid.Result };
        }

        public static async Task<KeycloakCredentials> UpdateCredentials(string controller, ITREClientHelper clientHelper, ModelStateDictionary modelState, KeycloakCredentials credentials)
        {
            if (modelState.IsValid)
            {
                var result =
                    await clientHelper.CallAPI<KeycloakCredentials, KeycloakCredentials>(
                        "/api/" + controller +"/UpdateCredentials", credentials);
                return result;
            }
            
            else
            {
                credentials.Valid = false;
                return credentials;
            }
        }

        public static async Task<bool> IsTRESynced(ITREClientHelper clientHelper)
        {
            BoolReturn result = await clientHelper.CallAPIWithoutModel<BoolReturn>("/api/Onboarding/IsTRESynced");
            return result.Result;
        }

        public static async Task<bool> IsConfigurationUploaded(ITREClientHelper clientHelper)
        {
            BoolReturn result = await clientHelper.CallAPIWithoutModel<BoolReturn>("/api/Onboarding/IsConfigurationUploaded");
            return result.Result;
        }

        public static async Task WipeVaultCredentials(ITREClientHelper clientHelper)
        {
            await clientHelper.CallAPIWithoutModel<BoolReturn>("/api/SubmissionCredentials/WipeVaultCredentials", httpMethod: HttpMethod.Post);
        }

        public static async Task<bool> IsUserAssignedTRE(ITREClientHelper clientHelper)
        {
            BoolReturn result = await clientHelper.CallAPIWithoutModel<BoolReturn>("/api/SubmissionCredentials/IsUserAssignedTRE");
            return result.Result;
        }
    }
}
