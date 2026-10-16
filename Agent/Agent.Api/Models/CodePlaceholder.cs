namespace Agent.Api.Models
{
    /// <summary>
    /// A single {{code}} or {{@code}} occurrence found in a TES message.
    /// </summary>
    public class CodePlaceholder
    {
        public CodePlaceholder(string code, string rawText, string location, bool isContainerImage)
        {
            Code = code;
            RawText = rawText;
            Location = location;
            IsContainerImage = isContainerImage;
        }

        /// <summary>
        /// The code used for the DMN lookup, lower-cased. Keeps the leading @ for quickcodes.
        /// </summary>
        public string Code { get; }

        /// <summary>
        /// The placeholder exactly as it appeared, for example "{{@sky-create}}".
        /// </summary>
        public string RawText { get; }

        /// <summary>
        /// Where in the message it was found, for example "executors[0].image".
        /// Used for diagnostics and to enforce the quickcode field restriction.
        /// </summary>
        public string Location { get; }

        /// <summary>
        /// True when this occurrence is in a container name field. Quickcodes are only
        /// allowed here.
        /// </summary>
        public bool IsContainerImage { get; }

        /// <summary>
        /// True for the {{@code}} quickcode syntax.
        /// </summary>
        public bool IsQuickcode => Code.StartsWith("@", StringComparison.Ordinal);
    }

    /// <summary>
    /// The outcome of substituting resolved values back into a TES message.
    /// </summary>
    public class CodeSubstitutionResult
    {
        public CodeSubstitutionResult(
            IReadOnlyList<CodePlaceholder> placeholders,
            IReadOnlyList<string> unresolvedCodes,
            IReadOnlyList<CodePlaceholder> misplacedQuickcodes)
        {
            Placeholders = placeholders;
            UnresolvedCodes = unresolvedCodes;
            MisplacedQuickcodes = misplacedQuickcodes;
        }

        /// <summary>
        /// Every placeholder occurrence encountered, in order.
        /// </summary>
        public IReadOnlyList<CodePlaceholder> Placeholders { get; }

        /// <summary>
        /// Distinct codes that had no value. Their occurrences were left blank.
        /// Any entry here fails the whole job.
        /// </summary>
        public IReadOnlyList<string> UnresolvedCodes { get; }

        /// <summary>
        /// Quickcode occurrences found outside a container name field. These are left as they
        /// were rather than substituted, and any entry here fails the whole job.
        /// </summary>
        public IReadOnlyList<CodePlaceholder> MisplacedQuickcodes { get; }

        public bool Success => UnresolvedCodes.Count == 0 && MisplacedQuickcodes.Count == 0;
    }
}
