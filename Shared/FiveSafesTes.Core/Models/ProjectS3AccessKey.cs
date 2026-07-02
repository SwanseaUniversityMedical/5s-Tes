namespace FiveSafesTes.Core.Models
{
    /// <summary>
    /// Full scoped S3 credential bundle for a Submission Layer project.
    /// Stored in Vault at S3Accesskeys/{projectId}.
    /// </summary>
    public class ProjectS3AccessKey
    {
        public int ProjectId { get; set; }
        public string ProjectName { get; set; } = string.Empty;
        /// <summary>S3 access key id (username) on Submission RustFS, e.g. proj-42-s3.</summary>
        public string AccessKey { get; set; } = string.Empty;
        /// <summary>S3 secret key paired with <see cref="AccessKey"/>.</summary>
        public string SecretKey { get; set; } = string.Empty;
        public string SubmissionBucket { get; set; } = string.Empty;
        public string OutputBucket { get; set; } = string.Empty;

        /// <summary>
        /// Builds a <see cref="ProjectS3AccessKey"/> from a Vault secret payload, or returns null
        /// when the payload is missing a usable access/secret key pair. Shared by every reader of
        /// the S3Accesskeys/{projectId} Vault path (Submission and TRE).
        /// </summary>
        /// <param name="projectId">Fallback project id used when the payload omits "projectId".</param>
        /// <param name="data">Raw key/value pairs returned by the Vault credentials service.</param>
        public static ProjectS3AccessKey? TryFromVault(int projectId, IReadOnlyDictionary<string, object> data)
        {
            if (data == null ||
                !data.TryGetValue("accessKey", out var accessKeyObj) ||
                !data.TryGetValue("secretKey", out var secretKeyObj) ||
                string.IsNullOrWhiteSpace(accessKeyObj?.ToString()) ||
                string.IsNullOrWhiteSpace(secretKeyObj?.ToString()))
            {
                return null;
            }

            return new ProjectS3AccessKey
            {
                ProjectId = data.TryGetValue("projectId", out var id) ? Convert.ToInt32(id) : projectId,
                ProjectName = data.TryGetValue("projectName", out var name) ? name?.ToString() ?? "" : "",
                AccessKey = accessKeyObj.ToString()!,
                SecretKey = secretKeyObj.ToString()!,
                SubmissionBucket = data.TryGetValue("submissionBucket", out var sub) ? sub?.ToString() ?? "" : "",
                OutputBucket = data.TryGetValue("outputBucket", out var output) ? output?.ToString() ?? "" : ""
            };
        }

        /// <summary>
        /// Serializes this credential bundle for storage in Vault. The optional
        /// <paramref name="timestampKey"/> records when/where it was written (e.g. "createdAt" on
        /// the Submission side, "syncedAt" on the TRE side).
        /// </summary>
        public Dictionary<string, object> ToVaultDictionary(string timestampKey, string timestampValue)
        {
            var dictionary = new Dictionary<string, object>
            {
                ["projectId"] = ProjectId,
                ["projectName"] = ProjectName,
                ["accessKey"] = AccessKey,
                ["secretKey"] = SecretKey,
                ["submissionBucket"] = SubmissionBucket,
                ["outputBucket"] = OutputBucket
            };

            if (!string.IsNullOrEmpty(timestampKey))
            {
                dictionary[timestampKey] = timestampValue;
            }

            return dictionary;
        }
    }
}
