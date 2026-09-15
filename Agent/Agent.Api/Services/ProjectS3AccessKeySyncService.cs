using FiveSafesTes.Core.Constants;
using FiveSafesTes.Core.Models;
using FiveSafesTes.Core.Services;
using Serilog;

namespace Agent.Api.Services
{
    /// <summary>
    /// Copies scoped Submission S3 credentials from the Submission Layer API into TRE Vault.
    /// Submission.Api creates the RustFS user/policy if they do not exist yet (EnsureAccessKeyAsync).
    /// </summary>
    public class ProjectS3AccessKeySyncService : IProjectS3AccessKeySyncService
    {
        private readonly IDareClientWithoutTokenHelper _dareClient;
        private readonly IVaultCredentialsService _vaultCredentialsService;

        public ProjectS3AccessKeySyncService(
            IDareClientWithoutTokenHelper dareClient,
            IVaultCredentialsService vaultCredentialsService)
        {
            _dareClient = dareClient;
            _vaultCredentialsService = vaultCredentialsService;
        }

        public async Task<ProjectS3AccessKey?> SyncProjectAccessKeyAsync(int projectId)
        {
            // Already in TRE Vault — no need to call Submission again (e.g. during periodic sync).
            var cached = await LoadFromTreVaultAsync(projectId);
            if (cached != null)
            {
                return cached;
            }

            if (!_dareClient.CheckCredsAreAvailable())
            {
                Log.Error(
                    "{Function} Cannot sync S3 credentials for project {ProjectId}: Submission credentials not configured",
                    nameof(SyncProjectAccessKeyAsync),
                    projectId);
                return null;
            }

            try
            {
                var projectS3Credentials = await _dareClient.CallAPIWithoutModel<ProjectS3AccessKey>(
                    $"/api/Project/GetProjectS3Credentials/{projectId}");

                if (projectS3Credentials == null ||
                    string.IsNullOrWhiteSpace(projectS3Credentials.AccessKey) ||
                    string.IsNullOrWhiteSpace(projectS3Credentials.SecretKey))
                {
                    Log.Warning(
                        "{Function} Submission Layer returned no S3 credentials for project {ProjectId}",
                        nameof(SyncProjectAccessKeyAsync),
                        projectId);
                    return null;
                }

                var stored = await _vaultCredentialsService.AddCredentialAsync(
                    S3AccessKeyVaultPaths.ForProject(projectId),
                    projectS3Credentials.ToVaultDictionary("syncedAt", DateTime.UtcNow.ToString("o")));

                if (!stored)
                {
                    Log.Error(
                        "{Function} Failed to store S3 credentials in TRE Vault for project {ProjectId}",
                        nameof(SyncProjectAccessKeyAsync),
                        projectId);
                    return null;
                }

                Log.Information(
                    "{Function} Synced scoped S3 credentials for project {ProjectId} into TRE Vault",
                    nameof(SyncProjectAccessKeyAsync),
                    projectId);

                return projectS3Credentials;
            }
            catch (Exception ex)
            {
                Log.Error(
                    ex,
                    "{Function} Failed to sync S3 credentials for project {ProjectId}",
                    nameof(SyncProjectAccessKeyAsync),
                    projectId);
                return null;
            }
        }

        private async Task<ProjectS3AccessKey?> LoadFromTreVaultAsync(int projectId)
        {
            var data = await _vaultCredentialsService.GetCredentialAsync(
                S3AccessKeyVaultPaths.ForProject(projectId));

            return ProjectS3AccessKey.TryFromVault(projectId, data);
        }
    }
}
