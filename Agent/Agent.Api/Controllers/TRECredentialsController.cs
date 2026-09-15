using Agent.Api.Repositories.DbContexts;
using Agent.Api.Services;
using FiveSafesTes.Core.Models;
using FiveSafesTes.Core.Models.APISimpleTypeReturns;
using FiveSafesTes.Core.Models.Settings;
using FiveSafesTes.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Agent.Api.Controllers
{
    [Route("api/[controller]")]

    [ApiController]
    public class TRECredentialsController : Controller
    {

        private readonly ApplicationDbContext _DbContext;
        private readonly IEncDecHelper _encDecHelper;
        private readonly KeycloakTokenHelper _keycloakTokenHelper;
        private readonly TreKeyCloakSettings _treKeyCloakSettings;

        public TRECredentialsController(ApplicationDbContext applicationDbContext, IEncDecHelper encDec, IOptions<TreKeyCloakSettings> treKeyCloakSettings)
        {
            _encDecHelper = encDec;
            _DbContext = applicationDbContext;
            _treKeyCloakSettings = treKeyCloakSettings.Value;
            _keycloakTokenHelper = new KeycloakTokenHelper(_treKeyCloakSettings.BaseUrl, _treKeyCloakSettings.ClientId,
              _treKeyCloakSettings.ClientSecret, _treKeyCloakSettings.Proxy, _treKeyCloakSettings.ProxyAddresURL, _treKeyCloakSettings.KeycloakDemoMode);
        }

        [Authorize(Roles = "dare-tre-admin")]
        [HttpGet("CheckCredentialsAreValid")]
        public async Task<BoolReturn> CheckCredentialsAreValidAsync()
        {
            return await ControllerHelpers.CheckCredentialsAreValid(_keycloakTokenHelper, _encDecHelper, _DbContext, CredentialType.Tre);
        }
        [Authorize(Roles = "dare-tre-admin")]
        [HttpPost("UpdateCredentials")]

        public async Task<KeycloakCredentials> UpdateCredentials(KeycloakCredentials creds)
        {
            creds = await ControllerHelpers.UpdateCredentials(creds, _keycloakTokenHelper, _DbContext, _encDecHelper, CredentialType.Tre, "dare-tre-admin");
            return creds;
        }
    }
}
