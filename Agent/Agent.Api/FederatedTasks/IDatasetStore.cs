namespace Agent.Api.FederatedTasks
{
    /// <summary>
    /// Object store access for dataset files.
    /// </summary>
    public interface IDatasetStore
    {
        Task<IReadOnlyList<string>> ListKeysAsync(string prefix, CancellationToken ct);

        Task<Stream> OpenReadAsync(string key, CancellationToken ct);
    }
}
