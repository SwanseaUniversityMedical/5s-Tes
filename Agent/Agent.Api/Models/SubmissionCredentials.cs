namespace Agent.Api.Models
{
    /// <summary>
    /// The environment variables fetched from Vault for one submission, split by who gets them.
    ///
    /// The credentials process stores a variable either at the base path for its credential
    /// type, meaning it applies to every executor, or one level deeper under an image code,
    /// meaning it applies only to executors running that image.
    /// </summary>
    public class SubmissionCredentials
    {
        /// <summary>
        /// Variables every executor receives, kept grouped by credential type (postgres,
        /// trino, s3, tre). The grouping matters because callers look a type up by name -
        /// the S3 block reads accessKey and secretKey out of the "s3" group specifically
        /// rather than trusting those names to be unique across every type.
        /// </summary>
        public Dictionary<string, Dictionary<string, string>> SharedByType { get; } =
            new(StringComparer.OrdinalIgnoreCase);

        /// <summary>Variables keyed by the image code they belong to.</summary>
        public Dictionary<string, Dictionary<string, string>> ByImageCode { get; } =
            new(StringComparer.Ordinal);

        public bool Any => SharedByType.Count > 0 || ByImageCode.Count > 0;

        /// <summary>Total variables across every group, for logging. Never log the values.</summary>
        public int Count => SharedByType.Values.Sum(group => group.Count)
                            + ByImageCode.Values.Sum(group => group.Count);

        /// <summary>The variables of one credential type, or an empty set if there are none.</summary>
        public IReadOnlyDictionary<string, string> OfType(string credentialType)
        {
            return SharedByType.TryGetValue(credentialType, out var group)
                ? group
                : new Dictionary<string, string>(StringComparer.Ordinal);
        }

        /// <summary>
        /// The variables one executor should receive: the shared ones, plus any belonging to
        /// the image codes it uses. An image code with no variables of its own is not an
        /// error - it just means nothing in the DMN was scoped to it.
        /// </summary>
        public IEnumerable<KeyValuePair<string, string>> ForImageCodes(IReadOnlyList<string> imageCodes)
        {
            foreach (var group in SharedByType.Values)
            {
                foreach (var pair in group)
                {
                    yield return pair;
                }
            }

            foreach (var imageCode in imageCodes)
            {
                if (!ByImageCode.TryGetValue(imageCode, out var scoped))
                {
                    continue;
                }

                foreach (var pair in scoped)
                {
                    yield return pair;
                }
            }
        }
    }
}
