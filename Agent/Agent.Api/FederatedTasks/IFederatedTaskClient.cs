using FiveSafesTes.Core.Models.FederatedTasks;
using FiveSafesTes.Core.Models.Tes;

namespace Agent.Api.FederatedTasks
{
    /// <summary>
    /// Talks to the Submission layer's federated task endpoints.
    /// </summary>
    public interface IFederatedTaskClient
    {
        /// <summary>
        /// False until Submission credentials have been uploaded.
        /// </summary>
        bool IsConfigured { get; }

        Task<List<TesTask>> GetPendingAsync();

        /// <summary>
        /// Returns true if Submission accepted the report.
        /// </summary>
        Task<bool> ReportAsync(FederatedTaskResultReport report);
    }
}
