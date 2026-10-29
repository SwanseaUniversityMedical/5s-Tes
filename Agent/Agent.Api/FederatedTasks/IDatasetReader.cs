namespace Agent.Api.FederatedTasks
{
    /// <summary>
    /// Finds and reads a dataset by name.
    /// </summary>
    public interface IDatasetReader
    {
        Task<DatasetReadResult> ReadAsync(string datasetName, CancellationToken ct);
    }
}
