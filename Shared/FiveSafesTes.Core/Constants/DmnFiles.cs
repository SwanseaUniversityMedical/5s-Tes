namespace FiveSafesTes.Core.Constants
{
    /// <summary>
    /// The DMN decision tables that TRE Admins edit and that get deployed to Zeebe.
    /// Their editable copies live under the configured DmnPath, which is a persistent
    /// volume in a deployed stack, so those copies win over the ones baked into the image.
    /// </summary>
    public static class DmnFiles
    {
        /// <summary>
        /// Environment variables for a submission, including the ephemeral credentials.
        /// Rows may be scoped to a single image code, or left unscoped to apply to every image.
        /// </summary>
        public const string EnvironmentVariables = "credentials.dmn";

        /// <summary>
        /// Resolves the {{@imageCode}} placeholder in an executor's image field to the
        /// container image to actually run.
        /// </summary>
        public const string ImageCodes = "imagecodes.dmn";

        /// <summary>Decision id of the environment variables table, as deployed to Zeebe.</summary>
        public const string EnvironmentVariablesDecisionId = "CredentialsDMN";

        /// <summary>Decision id of the image code table, as deployed to Zeebe.</summary>
        public const string ImageCodesDecisionId = "ImageCodesDMN";

        /// <summary>Every DMN file whose editable copy lives under the configured DmnPath.</summary>
        public static readonly IReadOnlyList<string> All = new[] { EnvironmentVariables, ImageCodes };
    }
}
