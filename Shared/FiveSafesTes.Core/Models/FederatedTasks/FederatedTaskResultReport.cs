using FiveSafesTes.Core.Models.Tes;

namespace FiveSafesTes.Core.Models.FederatedTasks
{
    /// <summary>
    /// The outcome a TRE Agent reports back to the orchestrator for a federated task.
    /// </summary>
    public class FederatedTaskResultReport
    {
        public string TaskId { get; set; } = string.Empty;
        public TesState State { get; set; }
        public decimal? Value { get; set; }
        public int? RowCount { get; set; }
        public string? ErrorCode { get; set; }
        public string? ErrorMessage { get; set; }
    }
}
