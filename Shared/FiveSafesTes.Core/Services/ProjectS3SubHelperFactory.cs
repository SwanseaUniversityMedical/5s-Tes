using FiveSafesTes.Core.Constants;
using FiveSafesTes.Core.Models;
using FiveSafesTes.Core.Models.ViewModels;
using Serilog;

namespace FiveSafesTes.Core.Services
{
    /// <summary>
    /// Builds an <see cref="IMinioSubHelper"/> configured with project-scoped Submission S3 credentials
    /// instead of the shared root credentials from environment variables.
    /// Used by the TRE Agent whenever it reads from or writes to the Submission Layer object store.
    /// </summary>
    public class ProjectS3SubHelperFactory : IProjectS3SubHelperFactory
    {
        private readonly IVaultCredentialsService _vaultCredentialsService;
        private readonly IProjectS3AccessKeySyncService _projectS3AccessKeySyncService;
        private readonly MinioSubSettings _baseSettings;

        public ProjectS3SubHelperFactory(
            IVaultCredentialsService vaultCredentialsService,
            IProjectS3AccessKeySyncService projectS3AccessKeySyncService,
            MinioSubSettings baseSettings)
        {
            _vaultCredentialsService = vaultCredentialsService;
            _projectS3AccessKeySyncService = projectS3AccessKeySyncService;
            _baseSettings = baseSettings;
        }

        public async Task<IMinioSubHelper> GetProjectS3HelperAsync(int submissionProjectId)
        {
            var credentials = await LoadFromVaultAsync(submissionProjectId);

            // Lazy sync: if TRE Vault is empty, fetch from Submission (which creates creds if needed).
            if (credentials == null)
            {
                Log.Information(
                    "Scoped Submission S3 credentials missing in TRE Vault for project {ProjectId}. Syncing from Submission Layer.",
                    submissionProjectId);
                credentials = await _projectS3AccessKeySyncService.SyncProjectAccessKeyAsync(submissionProjectId);
            }

            if (credentials == null ||
                string.IsNullOrWhiteSpace(credentials.AccessKey) ||
                string.IsNullOrWhiteSpace(credentials.SecretKey))
            {
                throw new InvalidOperationException(
                    $"Unable to resolve scoped Submission S3 credentials for project {submissionProjectId}. " +
                    "Ensure the project exists on the Submission Layer and the TRE is assigned to it.");
            }

            return new MinioSubHelper(CreateScopedSettings(credentials));
        }

        private async Task<ProjectS3AccessKey?> LoadFromVaultAsync(int submissionProjectId)
        {
            var path = S3AccessKeyVaultPaths.ForProject(submissionProjectId);
            var data = await _vaultCredentialsService.GetCredentialAsync(path);

            return ProjectS3AccessKey.TryFromVault(submissionProjectId, data);
        }

        /// <summary>
        /// Copies shared MinioSubSettings (URL, region, proxy, etc.) from config but replaces
        /// the root AccessKey/SecretKey with this project's scoped credentials from Vault.
        /// </summary>
        private MinioSubSettings CreateScopedSettings(ProjectS3AccessKey credentials) =>
            new()
            {
                Url = _baseSettings.Url,
                AccessKey = credentials.AccessKey,
                SecretKey = credentials.SecretKey,
                BucketName = _baseSettings.BucketName,
                Alias = _baseSettings.Alias,
                AWSRegion = _baseSettings.AWSRegion,
                AWSService = _baseSettings.AWSService,
                AdminConsole = _baseSettings.AdminConsole,
                HutchURLOverride = _baseSettings.HutchURLOverride,
                AttributeName = _baseSettings.AttributeName,
                UesProxy = _baseSettings.UesProxy,
                ProxyAddresURL = _baseSettings.ProxyAddresURL,
                ProxyAddresURLForExternalFetch = _baseSettings.ProxyAddresURLForExternalFetch,
                BypassProxy = _baseSettings.BypassProxy
            };
    }
}
