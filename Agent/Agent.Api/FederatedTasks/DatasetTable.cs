namespace Agent.Api.FederatedTasks
{
    /// <summary>
    /// A parsed dataset: column names plus rows, each row aligned with <see cref="Columns"/>.
    /// </summary>
    public sealed record DatasetTable(IReadOnlyList<string> Columns, IReadOnlyList<IReadOnlyList<string?>> Rows);
}
