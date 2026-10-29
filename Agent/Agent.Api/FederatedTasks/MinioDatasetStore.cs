using Agent.Api.Models;
using FiveSafesTes.Core.Services;

namespace Agent.Api.FederatedTasks
{
    /// <summary>
    /// Reads dataset files from the TRE dataset bucket.
    /// </summary>
    public class MinioDatasetStore : IDatasetStore
    {
        private readonly IMinioTreHelper _helper;
        private readonly FederatedTaskSettings _settings;

        public MinioDatasetStore(IMinioTreHelper helper, FederatedTaskSettings settings)
        {
            _helper = helper;
            _settings = settings;
        }

        public async Task<IReadOnlyList<string>> ListKeysAsync(string prefix, CancellationToken ct)
        {
            var response = await _helper.GetFilesInBucket(_settings.DatasetBucket, prefix);
            return response?.S3Objects.Select(o => o.Key).ToList() ?? [];
        }

        public async Task<Stream> OpenReadAsync(string key, CancellationToken ct)
        {
            var response = await _helper.GetCopyObject(_settings.DatasetBucket, key);
            return response.ResponseStream;
        }
    }
}
