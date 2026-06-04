using Credentials.Models.Services;
using Microsoft.AspNetCore.Mvc;

namespace Credentials.Camunda.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CredentialsController : ControllerBase
{
    private readonly IServicedZeebeClient _zeebeClient;

    public CredentialsController(IServicedZeebeClient zeebeClient)
    {
        _zeebeClient = zeebeClient;
    }

    [HttpPost("start")]
    public async Task<IActionResult> StartCredentials([FromBody] StartCredentialsRequest[] records)
    {
        var variables = new Dictionary<string, object>
        {
            ["project"] = records[0].Project,
            ["user"] = records[0].User,
            ["submissionId"] = records[0].SubmissionId,
            ["InputCollections"] = records.Select(r => new Dictionary<string, object>
            {
                ["project"] = r.Project,
                ["user"] = r.User,
                ["submissionId"] = r.SubmissionId
            }).ToList()
        };

        await _zeebeClient.CreateProcessInstanceAsync("Start_Credentials", variables);
        return Ok();
    }

    [HttpPost("revoke")]
    public async Task<IActionResult> RevokeCredentials([FromBody] RevokeCredentialsRequest[] records)
    {
        var variables = new Dictionary<string, object>
        {
            ["submissionId"] = records[0].SubmissionId,
            ["project"] = records[0].Project,
            ["user"] = records[0].User,
            ["timer"] = records[0].Timer,
            ["InputCollections"] = records.Select(r => new Dictionary<string, object>
            {
                ["submissionId"] = r.SubmissionId,
                ["project"] = r.Project,
                ["user"] = r.User,
                ["timer"] = r.Timer
            }).ToList()
        };

        await _zeebeClient.CreateProcessInstanceAsync("Credentials_Revoke", variables);
        return Ok();
    }
}

public record StartCredentialsRequest(string Project, string User, string SubmissionId);
public record RevokeCredentialsRequest(string SubmissionId, string Project, string User, int Timer);
