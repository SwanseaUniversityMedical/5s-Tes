namespace FiveSafesTes.Core.Services
{
    public interface IProjectMinioSubHelperFactory
    {
        Task<IMinioSubHelper> GetForProjectAsync(int submissionProjectId);
    }
}
