using Credentials.Camunda.Services;
using Zeebe.Client.Accelerator.Abstractions;
using Zeebe.Client.Accelerator.Attributes;

namespace Credentials.Camunda.ProcessHandlers
{
    /// <summary>
    /// "Move Secret Ones to Project Vault" in Codes_resolve_sub.
    ///
    /// For every code whose DMN rule gave a vaultPath, reads the real secret from that path
    /// and copies it into project/taskId alongside the plain values. After this the Agent can
    /// read every code it needs, secret or not, from one place in a single call.
    ///
    /// The secret value itself is never logged.
    /// </summary>
    [JobType("move-secret-codes-to-vault")]
    public class MoveSecretCodesToVaultHandler : CodesHandlerBase, IAsyncZeebeWorker
    {
        private readonly ILogger<MoveSecretCodesToVaultHandler> _logger;

        public MoveSecretCodesToVaultHandler(ICodeVaultService codeVault, ILogger<MoveSecretCodesToVaultHandler> logger)
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

            var secretValues = new Dictionary<string, string>();
            var unreadable = new List<string>();

            foreach (var resolved in variables.resolvedCodes!)
            {
                if (string.IsNullOrWhiteSpace(resolved.code) || !IsSecret(resolved))
                {
                    continue;
                }

                var secret = await CodeVault.ReadSourceSecretAsync(resolved.result!.vaultPath!, resolved.code);

                // Store nothing when the secret cannot be read. The Agent finds the code
                // missing and fails the job, which is the behaviour a failed vault lookup
                // is meant to produce.
                if (string.IsNullOrEmpty(secret))
                {
                    unreadable.Add(resolved.code);
                    continue;
                }

                secretValues[resolved.code] = secret;
            }

            if (unreadable.Count > 0)
            {
                _logger.LogError(
                    "Could not read {Count} secret(s) from vault, which will fail the job at substitution time: {Codes}",
                    unreadable.Count, string.Join(", ", unreadable));
            }

            await CodeVault.MergeCodesAsync(variables.project!, variables.taskId!, secretValues);
        }
    }
}
