using FiveSafesTes.Core.Models;

namespace FiveSafesTes.Core.Services
{
    /// <summary>
    /// Fetches scoped Submission S3 credentials from GET /api/Project/GetProjectS3Credentials
    /// and stores them in TRE Vault.
    /// </summary>
    public interface IProjectS3AccessKeySyncService
    {
        Task<ProjectS3AccessKey?> SyncProjectAccessKeyAsync(int projectId);
    }
}
