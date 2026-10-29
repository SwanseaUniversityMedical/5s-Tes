using Agent.Api.FederatedTasks;

namespace Agent.Api.Tests.FederatedTasks;

public class FakeDatasetStore : IDatasetStore
{
    private readonly Dictionary<string, byte[]> _objects = new();

    public int ListCalls { get; private set; }

    public string? ThrowOnOpenKey { get; set; }

    public FakeDatasetStore Add(string key, string content) => Add(key, System.Text.Encoding.UTF8.GetBytes(content));

    public FakeDatasetStore Add(string key, byte[] content)
    {
        _objects[key] = content;
        return this;
    }

    public Task<IReadOnlyList<string>> ListKeysAsync(string prefix, CancellationToken ct)
    {
        ListCalls++;
        IReadOnlyList<string> keys = _objects.Keys.Where(k => k.StartsWith(prefix, StringComparison.Ordinal)).ToList();
        return Task.FromResult(keys);
    }

    public Task<Stream> OpenReadAsync(string key, CancellationToken ct)
    {
        if (key == ThrowOnOpenKey)
        {
            throw new IOException($"cannot open {key}");
        }

        return Task.FromResult<Stream>(new MemoryStream(_objects[key]));
    }
}
