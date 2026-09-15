using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Submission.Api.Attributes;
using Swashbuckle.AspNetCore.Annotations;

namespace Submission.Api.Controllers;

[Route("api/[controller]")]
[ApiController]
public class HealthCheckController : ControllerBase
{
    [HttpGet("CheckHealth")]
    [AllowAnonymous]
    [ValidateModelState]
    [SwaggerOperation("CheckHealth")]
    [SwaggerResponse(statusCode: 200)]
    public virtual IActionResult CheckHealth()
    {
        return StatusCode(200);
    }

}
