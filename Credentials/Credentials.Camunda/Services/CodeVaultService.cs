namespace Credentials.Camunda.Services
{
    public interface ICodeVaultService
    {
        /// <summary>
        /// Adds the given code/value pairs to the project's store for one task, keeping
        /// anything already there.
        /// </summary>
        Task<bool> MergeCodesAsync(string project, string taskId, IReadOnlyDictionary<string, string> codeValues);

        /// <summary>
        /// Reads the actual secret a DMN vaultPath points at.
        /// Returns null when the secret or its value cannot be found.
        /// </summary>
        Task<string?> ReadSourceSecretAsync(string vaultPath, string code);
    }

    /// <summary>
    /// Stores resolved codes in Vault so the Agent can read them all back in one call at
    /// substitution time.
    ///
    /// Everything for one task lands at project/taskId keyed by code: plain values directly,
    /// and secrets copied in from wherever the DMN's vaultPath points. taskId scopes the path
    /// because it is unique per submission, so two submissions in the same project cannot
    /// overwrite each other.
    ///
    /// Resolved values are never logged. Codes and paths are names, not secrets, so they are.
    /// </summary>
    public class CodeVaultService : ICodeVaultService
    {
        private readonly IVaultCredentialsService _vault;
        private readonly ILogger<CodeVaultService> _logger;

        public CodeVaultService(IVaultCredentialsService vault, ILogger<CodeVaultService> logger)
        {
            _vault = vault;
            _logger = logger;
        }

        public async Task<bool> MergeCodesAsync(string project, string taskId,
            IReadOnlyDictionary<string, string> codeValues)
        {
            if (codeValues.Count == 0)
            {
                return true;
            }

            var path = BuildCodePath(project, taskId);

            // Writing to a KV v2 path replaces the whole secret, so read what is already
            // there and write the union back. Both handlers write to this same path, and
            // this also makes a retry of either one harmless.
            var existing = await _vault.GetCredentialAsync(path);
            var merged = new Dictionary<string, object>(existing);

            foreach (var pair in codeValues)
            {
                merged[pair.Key] = pair.Value;
            }

            var stored = await _vault.AddCredentialAsync(path, merged);

            if (stored)
            {
                _logger.LogInformation("Stored {Count} code(s) at vault path {Path}: {Codes}",
                    codeValues.Count, path, string.Join(", ", codeValues.Keys));
            }
            else
            {
                _logger.LogError("Failed to store {Count} code(s) at vault path {Path}",
                    codeValues.Count, path);
            }

            return stored;
        }

        public async Task<string?> ReadSourceSecretAsync(string vaultPath, string code)
        {
            var path = NormalisePath(vaultPath);
            if (path.Length == 0)
            {
                return null;
            }

            var secret = await _vault.GetCredentialAsync(path);

            if (secret.Count == 0)
            {
                _logger.LogError("No secret found at vault path {Path} for code {Code}", path, code);
                return null;
            }

            // The code is the key by convention, the same way resolved codes are stored.
            if (secret.TryGetValue(code, out var byCode))
            {
                return byCode?.ToString();
            }

            // A source secret provisioned by hand may hold a single unnamed value instead.
            if (secret.Count == 1)
            {
                _logger.LogInformation(
                    "Secret at {Path} has no '{Code}' field, using its single field '{Field}'",
                    path, code, secret.Keys.First());

                return secret.Values.First()?.ToString();
            }

            _logger.LogError(
                "Secret at {Path} has no '{Code}' field and holds {Count} fields, so the value is ambiguous. Fields: {Fields}",
                path, code, secret.Count, string.Join(", ", secret.Keys));

            return null;
        }

        /// <summary>
        /// Where every resolved code for one task is stored: project/taskId.
        /// </summary>
        private static string BuildCodePath(string project, string taskId)
        {
            return $"{NormalisePath(project)}/{NormalisePath(taskId)}";
        }

        /// <summary>
        /// The vault client builds v1/{engine}/data/{path}, so a leading slash on a DMN
        /// vaultPath would produce a double slash and a 404.
        /// </summary>
        private static string NormalisePath(string? path)
        {
            return string.IsNullOrWhiteSpace(path) ? string.Empty : path.Trim().Trim('/');
        }
    }
}
