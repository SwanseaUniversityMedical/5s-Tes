namespace FiveSafesTes.Core.Models.FederatedTasks
{
    /// <summary>
    /// A row filter: <paramref name="Column"/> compared to <paramref name="Value"/> with <paramref name="Op"/>
    /// (eq, gt, lt, gte or lte).
    /// </summary>
    public sealed record TaskFilter(string Column, string Op, string Value);
}
