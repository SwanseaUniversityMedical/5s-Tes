using FiveSafesTes.Core.Models;

namespace FiveSafesTes.Core.Services
{
    public interface IProjectS3AccessKeyService
    {
        Task<ProjectS3AccessKey?> EnsureAccessKeyAsync(int projectId, string projectName, string submissionBucket, string outputBucket);
        Task<ProjectS3AccessKey?> GetAccessKeyAsync(int projectId);
    }
}
