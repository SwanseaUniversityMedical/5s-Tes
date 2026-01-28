using Agent.Api.Repositories.DbContexts;
using Agent.Api.Services;
using FiveSafesTes.Core.Models;
using FiveSafesTes.Core.Models.APISimpleTypeReturns;
using FiveSafesTes.Core.Models.Settings;
using FiveSafesTes.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Agent.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class DataEgressCredentialsController : Controller
    {

        private readonly ApplicationDbContext _DbContext;
        private readonly IEncDecHelper _encDecHelper;
        public KeycloakTokenHelper _keycloakTokenHelper { get; set; }
        

        public DataEgressCredentialsController(ApplicationDbContext applicationDbContext, IEncDecHelper encDec, DataEgressKeyCloakSettings keycloakSettings)
        {
            _encDecHelper = encDec;
            _DbContext = applicationDbContext;
            _keycloakTokenHelper = new KeycloakTokenHelper(keycloakSettings.BaseUrl, keycloakSettings.ClientId,
                keycloakSettings.ClientSecret, keycloakSettings.Proxy, keycloakSettings.ProxyAddresURL, keycloakSettings.KeycloakDemoMode);
        }

        [Authorize(Roles = "dare-tre-admin")]
        [HttpGet("CheckCredentialsAreValid")]
        public async Task<BoolReturn> CheckCredentialsAreValidAsync()
        {
            return await ControllerHelpers.CheckCredentialsAreValid(_keycloakTokenHelper, _encDecHelper, _DbContext, CredentialType.Egress);
            

        }

        [Authorize(Roles = "dare-tre-admin")]
        [HttpPost("UpdateCredentials")]
        public async Task<KeycloakCredentials> UpdateCredentials(KeycloakCredentials creds)
        {
            creds = await ControllerHelpers.UpdateCredentials(creds, _keycloakTokenHelper, _DbContext, _encDecHelper, CredentialType.Egress, "dare-tre-admin");
            return creds;
        }

        
    }
}
