using System.Text.RegularExpressions;

namespace FiveSafesTes.Core.Models.FederatedTasks
{
    /// <summary>
    /// Naming rules shared by the federated task contract.
    /// </summary>
    public static class FederatedTaskNames
    {
        private static readonly Regex DatasetNamePattern =
            new("^[a-z0-9][a-z0-9_-]{0,62}$", RegexOptions.CultureInvariant);

        /// <summary>
        /// True when the name is a safe dataset folder name: lowercase letters, digits, '_' and '-',
        /// starting with a letter or digit, at most 63 characters.
        /// </summary>
        public static bool IsValidDatasetName(string? name) =>
            name != null && DatasetNamePattern.IsMatch(name);
    }
}
