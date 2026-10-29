namespace Agent.Api.FederatedTasks
{
    /// <summary>
    /// Result of running an operation: a value (null when no rows matched) and the number of rows it covers,
    /// or an error code and message.
    /// </summary>
    public sealed record OperationResult(decimal? Value, int RowCount, string? ErrorCode, string? ErrorMessage)
    {
        public bool IsSuccess => ErrorCode == null;
    }
}
