using Agent.Api.Models;
using Agent.Api.Services;
using Microsoft.AspNetCore.Mvc;
using HttpGetAttribute = Microsoft.AspNetCore.Mvc.HttpGetAttribute;

namespace Agent.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class HasuraAuthenticationController : Controller
    {
        private readonly IHasuraAuthenticationService _hasuraAuthenticationService;

        public HasuraAuthenticationController(AuthenticationSettings AuthenticationSettings, IHasuraAuthenticationService hasuraAuthenticationService)
        {        
            _hasuraAuthenticationService = hasuraAuthenticationService;
        }

        [HttpGet("")]
        public string Index([FromHeader] string Token)
        {
            return _hasuraAuthenticationService.CheckeTokenAndGetRoles(Token);
        }
    }
}
