using System.Text.Json;
using Zeebe.Client.Accelerator.Abstractions;
using Zeebe.Client.Accelerator.Attributes;


namespace Credentials.Camunda.ProcessHandlers
{

    [JobType("storeParentKey")]
    public class StoreParentKeyHandler : IAsyncZeebeWorkerWithResult<Dictionary<string, object>>
    {
        private readonly ILogger<StoreParentKeyHandler> _logger;

        public StoreParentKeyHandler(ILogger<StoreParentKeyHandler> logger)
        {
            _logger = logger;
        }

        public Task<Dictionary<string, object>> HandleJob(ZeebeJob job, CancellationToken cancellationToken)
        {
            //var vars = JsonSerializer.Deserialize<Dictionary<string, object>>(job.Variables) ?? new();
            //if (!vars.TryGetValue("submissionId", out var sub) || sub is null)
            //    throw new InvalidOperationException("submissionId missing");

            var parentKey = job.ProcessInstanceKey;
            _logger.LogInformation("Set parentProcessKey {Key}", parentKey);

            return Task.FromResult(new Dictionary<string, object>
            {
                ["parentProcessKey"] = parentKey
            });
        }
    }
}
