namespace FiveSafesTes.Core.Constants
{
    /// <summary>
    /// The DMN decision tables that TRE Admins edit and that get deployed to Zeebe.
    /// Their editable copies live under the configured DmnPath, which is a persistent
    /// volume in a deployed stack, so those copies win over the ones baked into the image.
    /// </summary>
    public static class DmnFiles
    {
        /// <summary>Ephemeral credentials for a submission. Outputs environment variables.</summary>
        public const string Credentials = "credentials.dmn";

        /// <summary>Resolves the codes behind {{code}} and {{@code}} placeholders in a TES message.</summary>
        public const string Codes = "codes.dmn";

        /// <summary>Decision id of the credentials table, as deployed to Zeebe.</summary>
        public const string CredentialsDecisionId = "CredentialsDMN";

        /// <summary>Decision id of the code resolution table, as deployed to Zeebe.</summary>
        public const string CodesDecisionId = "CodesDMN";

        /// <summary>Every DMN file whose editable copy lives under the configured DmnPath.</summary>
        public static readonly IReadOnlyList<string> All = new[] { Credentials, Codes };
    }
}
