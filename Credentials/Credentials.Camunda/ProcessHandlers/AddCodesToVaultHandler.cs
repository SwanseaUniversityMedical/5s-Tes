using Credentials.Camunda.Services;
using Zeebe.Client.Accelerator.Abstractions;
using Zeebe.Client.Accelerator.Attributes;

namespace Credentials.Camunda.ProcessHandlers
{
    /// <summary>
    /// "Add Codes to Vault Project" in Codes_resolve_sub.
    ///
    /// Stores every plain code value for the task at project/taskId. Secrets are left to
    /// MoveSecretCodesToVaultHandler, which copies them in from wherever the DMN points.
    /// </summary>
    [JobType("add-codes-to-vault")]
    public class AddCodesToVaultHandler : CodesHandlerBase, IAsyncZeebeWorker
    {
        private readonly ILogger<AddCodesToVaultHandler> _logger;

        public AddCodesToVaultHandler(ICodeVaultService codeVault, ILogger<AddCodesToVaultHandler> logger)
            : base(codeVault, logger)
        {
            _logger = logger;
        }

        public async Task HandleJob(ZeebeJob job, CancellationToken cancellationToken)
        {
            var variables = ReadVariables(job);
            if (variables is null)
            {
                return;
            }

            var plainValues = new Dictionary<string, string>();
            var unmatched = new List<string>();

            foreach (var resolved in variables.resolvedCodes!)
            {
                if (string.IsNullOrWhiteSpace(resolved.code) || IsSecret(resolved))
                {
                    continue;
                }

                // No rule matched, or the rule gave neither a value nor a vault path. Store
                // nothing: the Agent will find the code missing and fail the job.
                if (string.IsNullOrEmpty(resolved.result?.codeValue))
                {
                    unmatched.Add(resolved.code);
                    continue;
                }

                plainValues[resolved.code] = resolved.result.codeValue!;
            }

            if (unmatched.Count > 0)
            {
                _logger.LogWarning(
                    "The codes DMN returned no value for {Count} code(s), which will fail the job at substitution time: {Codes}",
                    unmatched.Count, string.Join(", ", unmatched));
            }

            await CodeVault.MergeCodesAsync(variables.project!, variables.taskId!, plainValues);
        }
    }
}
