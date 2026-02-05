using FiveSafesTes.Core.Models;
using FiveSafesTes.Core.Models.APISimpleTypeReturns;
using FiveSafesTes.Core.Services;
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
    }
}
