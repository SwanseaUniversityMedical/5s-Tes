using System.Text.Json;
using Credentials.Camunda.Models;
using Credentials.Camunda.Services;
using Zeebe.Client.Accelerator.Abstractions;

namespace Credentials.Camunda.ProcessHandlers
{
    /// <summary>
    /// Shared plumbing for the two Codes_resolve_sub service tasks. Both read the same job
    /// variables and both write into the same project/taskId store.
    ///
    /// Neither handler throws when a code cannot be stored. A code that is missing from the
    /// store is detected by the Agent at substitution time and fails the whole job there,
    /// which is where the failure belongs. Throwing here would instead leave a Camunda
    /// incident and a process instance stuck waiting for someone to retry it.
    /// </summary>
    public abstract class CodesHandlerBase
    {
        protected readonly ICodeVaultService CodeVault;
        private readonly ILogger _logger;

        protected CodesHandlerBase(ICodeVaultService codeVault, ILogger logger)
        {
            CodeVault = codeVault;
            _logger = logger;
        }

        /// <summary>
        /// The job variables the Codes subprocess carries.
        /// </summary>
        protected class CodesJobVariables
        {
            public string? project { get; set; }

            public string? taskId { get; set; }

            public List<ResolvedCode>? resolvedCodes { get; set; }
        }

        /// <summary>
        /// Reads and validates the job variables. Returns null and logs when the job has
        /// nothing to do, which is the normal case for a message with no placeholders.
        /// </summary>
        protected CodesJobVariables? ReadVariables(ZeebeJob job)
        {
            CodesJobVariables? variables;

            try
            {
                variables = JsonSerializer.Deserialize<CodesJobVariables>(job.Variables);
            }
            catch (JsonException exception)
            {
                _logger.LogError(exception, "Could not read the code resolution job variables");
                return null;
            }

            if (variables?.resolvedCodes is null || variables.resolvedCodes.Count == 0)
            {
                _logger.LogInformation("No resolved codes to store for this task");
                return null;
            }

            if (string.IsNullOrWhiteSpace(variables.project) || string.IsNullOrWhiteSpace(variables.taskId))
            {
                _logger.LogError(
                    "Cannot store codes without both project and taskId. project={Project}, taskId={TaskId}",
                    variables.project, variables.taskId);

                return null;
            }

            return variables;
        }

        /// <summary>
        /// A code is a secret when the DMN gave it a vaultPath to fetch the value from,
        /// rather than a plain codeValue.
        /// </summary>
        protected static bool IsSecret(ResolvedCode resolved)
        {
            return !string.IsNullOrWhiteSpace(resolved.result?.vaultPath);
        }
    }
}
