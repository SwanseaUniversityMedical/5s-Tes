namespace FiveSafesTes.Core.Services
{
    public interface IProjectS3SubHelperFactory
    {
        Task<IMinioSubHelper> GetProjectS3HelperAsync(int submissionProjectId);
    }
}
