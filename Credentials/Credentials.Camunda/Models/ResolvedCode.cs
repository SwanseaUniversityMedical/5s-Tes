namespace Credentials.Camunda.Models
{
    /// <summary>
    /// One entry of the resolvedCodes collection produced by the Codes DMN task.
    /// Property names match the FEEL output element in Codes.bpmn.
    /// </summary>
    public class ResolvedCode
    {
        public string code { get; set; } = string.Empty;

        /// <summary>
        /// The decision output, or null when no rule matched the code. A code with no
        /// decision is written nowhere, so the Agent finds it missing and fails the job.
        /// </summary>
        public CodeDecision? result { get; set; }
    }

    /// <summary>
    /// The Codes DMN output columns. Exactly one of these carries the answer: a non-empty
    /// vaultPath means the value is a secret to be fetched from there, otherwise codeValue
    /// is the plain value.
    /// </summary>
    public class CodeDecision
    {
        public string? codeValue { get; set; }

        public string? vaultPath { get; set; }
    }
}
