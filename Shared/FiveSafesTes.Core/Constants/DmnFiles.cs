namespace FiveSafesTes.Core.Constants
{
    /// <summary>
    /// One DMN decision table that TRE Admins edit and that gets deployed to Zeebe.
    /// </summary>
    /// <param name="Slug">Identifies the table in an API request.</param>
    /// <param name="FileName">The file under the configured DmnPath.</param>
    /// <param name="DecisionId">The decision id as deployed to Zeebe.</param>
    /// <param name="DisplayName">What to call it in the admin UI.</param>
    public sealed record DmnTable(string Slug, string FileName, string DecisionId, string DisplayName);

    /// <summary>
    /// The DMN decision tables that TRE Admins edit and that get deployed to Zeebe.
    /// Their editable copies live under the configured DmnPath, which is a persistent
    /// volume in a deployed stack, so those copies win over the ones baked into the image.
    /// </summary>
    public static class DmnFiles
    {
        /// <summary>
        /// Environment variables for a submission, including the ephemeral credentials.
        /// Rows may be scoped to image codes, or left unscoped to apply to every image.
        /// The file and decision id keep their original names: the decision id is referenced
        /// by Credentials.bpmn and by every already-deployed copy, and renaming the file
        /// would leave the old one behind on the volume to be deployed alongside it.
        /// </summary>
        public static readonly DmnTable EnvironmentVariablesTable =
            new("environment-variables", "credentials.dmn", "CredentialsDMN", "Environment Variables");

        /// <summary>
        /// Resolves the {{@imageCode}} placeholder in an executor's image field to the
        /// container image to actually run.
        /// </summary>
        public static readonly DmnTable ImageCodesTable =
            new("image-codes", "imagecodes.dmn", "ImageCodesDMN", "Image Codes");

        /// <summary>Every table, in the order the admin UI should offer them.</summary>
        public static readonly IReadOnlyList<DmnTable> Tables =
            new[] { EnvironmentVariablesTable, ImageCodesTable };

        public const string EnvironmentVariables = "credentials.dmn";
        public const string ImageCodes = "imagecodes.dmn";
        public const string EnvironmentVariablesDecisionId = "CredentialsDMN";
        public const string ImageCodesDecisionId = "ImageCodesDMN";

        /// <summary>Every DMN file whose editable copy lives under the configured DmnPath.</summary>
        public static readonly IReadOnlyList<string> All =
            Tables.Select(table => table.FileName).ToList();

        /// <summary>
        /// The table a request is asking for. An unknown or missing slug gives the
        /// environment variables table, which is what the admin UI asked for before it
        /// could address more than one.
        /// </summary>
        public static DmnTable Resolve(string? slug)
        {
            if (string.IsNullOrWhiteSpace(slug))
            {
                return EnvironmentVariablesTable;
            }

            return Tables.FirstOrDefault(
                       table => string.Equals(table.Slug, slug.Trim(), StringComparison.OrdinalIgnoreCase))
                   ?? EnvironmentVariablesTable;
        }

        /// <summary>
        /// Where a DMN file's editable copy lives, given the configured DmnPath.
        /// A configured path may be absolute, as it is in a deployed stack where it points
        /// at the persistent volume, or relative for local development.
        /// </summary>
        public static string ResolvePath(string? configuredPath, string fileName)
        {
            if (!string.IsNullOrEmpty(configuredPath))
            {
                if (Path.IsPathRooted(configuredPath))
                {
                    return Path.Combine(configuredPath, fileName);
                }

                var configuredProjectDirectory =
                    Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", ".."));

                return Path.GetFullPath(Path.Combine(configuredProjectDirectory, configuredPath, fileName));
            }

            var projectDirectory =
                Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", ".."));

            return Path.GetFullPath(Path.Combine(projectDirectory, "..", "..",
                "Credentials", "Credentials.Models", "ProcessModels", fileName));
        }
    }
}
