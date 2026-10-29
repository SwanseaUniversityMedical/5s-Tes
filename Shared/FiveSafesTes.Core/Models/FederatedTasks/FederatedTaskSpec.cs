namespace FiveSafesTes.Core.Models.FederatedTasks
{
    /// <summary>
    /// A validated federated task: one allow-listed operation over one dataset.
    /// </summary>
    public sealed record FederatedTaskSpec(
        string Operation,
        string Dataset,
        string? Column,
        IReadOnlyList<TaskFilter> Filters,
        string StudyId,
        string TreName);

    /// <summary>
    /// Outcome of parsing a TES task against the federated profile: either a spec or an error code and message.
    /// </summary>
    public sealed record FederatedTaskParseResult(FederatedTaskSpec? Spec, string? ErrorCode, string? ErrorMessage)
    {
        public bool IsSuccess => Spec != null;
    }
}
