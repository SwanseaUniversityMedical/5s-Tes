using FiveSafesTes.Core.Models.APISimpleTypeReturns;

namespace Agent.Api.Services
{
    public interface IDareSyncHelper
    {
        Task<BoolReturn> SyncSubmissionWithTre();
    }
}
