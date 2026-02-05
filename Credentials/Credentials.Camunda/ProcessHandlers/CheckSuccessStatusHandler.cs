using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using Credentials.Models.DbContexts;
using Credentials.Models.Models;
using Credentials.Models.Models.Zeebe;
using Zeebe.Client.Accelerator.Abstractions;
using Zeebe.Client.Accelerator.Attributes;

namespace Credentials.Camunda.ProcessHandlers
{
    [JobType("set-success-status")]
    public class SetSuccessStatusHandler : IAsyncZeebeWorker
    {
        private readonly CredentialsDbContext _credDb;
        private readonly ILogger<SetSuccessStatusHandler> _logger;

        public SetSuccessStatusHandler(CredentialsDbContext credsDb, ILogger<SetSuccessStatusHandler> logger)
        {
            _credDb = credsDb;
            _logger = logger;
        }

        public async Task HandleJob(ZeebeJob job, CancellationToken token)
        {
            var vars = JsonSerializer.Deserialize<Dictionary<string, object>>(job.Variables) ?? new();

            if (!vars.TryGetValue("parentProcessKey", out var parentObj) || parentObj is null)
            {
                _logger.LogWarning("parentProcessKey missing, cannot check for success status.");
                return;
            }

            var variables = JsonSerializer.Deserialize<Dictionary<string, object>>(job.Variables);
            var envListJson = variables["envList"]?.ToString();
            var envList = JsonSerializer.Deserialize<List<CredentialsCamundaOutput>>(envListJson);
            var subItem = envList.FirstOrDefault(x => string.Equals(x.env, "submissionId", StringComparison.OrdinalIgnoreCase));
            var submissionId = subItem?.value;
            long parentProcessKey = parentObj is JsonElement el && el.ValueKind == JsonValueKind.Number && el.TryGetInt64(out var parsed)
                ? parsed
                : long.Parse(parentObj.ToString());

            var rows = await _credDb.EphemeralCredentials.Where(e => e.ParentProcessInstanceKey == parentProcessKey).ToListAsync();

            if (rows.Count == 0)
            {
                
                _logger.LogWarning("No rows found for ParentProcessInstanceKey={Parent}. Hence error.", parentProcessKey);
                _credDb.EphemeralCredentials.Add(new EphemeralCredential
                {
                    ParentProcessInstanceKey = parentProcessKey,
                    ProcessInstanceKey = job.ProcessInstanceKey,
                    SubmissionId = int.Parse(submissionId),
                    CreatedAt = DateTime.UtcNow,
                    IsProcessed = false,
                    SuccessStatus = SuccessStatus.Error,
                    ErrorMessage = "No credential records found."
                });
                await _credDb.SaveChangesAsync();
                return;
            }

            bool anyError = rows.Any(r => r.SuccessStatus == SuccessStatus.Error);

            if (anyError)
            {
                _logger.LogInformation("Status ERROR for ParentProcessInstanceKey={Parent}", parentProcessKey);               
                return;
            }

            
            foreach (var r in rows)
                r.SuccessStatus = SuccessStatus.Success;

            await _credDb.SaveChangesAsync();
            _logger.LogInformation("Status SUCCESS  for ParentProcessInstanceKey={Parent}", parentProcessKey);
        }
    }
}
