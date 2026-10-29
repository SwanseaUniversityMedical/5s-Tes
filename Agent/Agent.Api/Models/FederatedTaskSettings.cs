namespace Agent.Api.Models
{
    /// <summary>
    /// Settings for federated task execution on the TRE Agent.
    /// </summary>
    public class FederatedTaskSettings
    {
        /// <summary>
        /// Dedicated TRE bucket holding the dataset CSVs that federated tasks read. Kept separate
        /// from project submission/output buckets so its access can be locked down on its own,
        /// and so expired-project bucket cleanup never touches it. Created at startup if missing.
        /// </summary>
        public string DatasetBucket { get; set; } = "federated-datasets";

        /// <summary>
        /// Key prefix under which each dataset lives, as {DatasetPrefix}{datasetName}/*.csv.
        /// </summary>
        public string DatasetPrefix { get; set; } = "dataset/";

        /// <summary>
        /// File extensions (without the dot) that the upload policy allows to be uploaded.
        /// </summary>
        public string[] AllowedExtensions { get; set; } = ["csv"];

        /// <summary>
        /// Name of the RustFS canned policy, created at startup, that grants dataset upload.
        /// </summary>
        public string UploadPolicyName { get; set; } = "dataset-upload";
    }
}
