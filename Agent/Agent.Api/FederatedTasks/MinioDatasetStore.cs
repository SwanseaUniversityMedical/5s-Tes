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
            if (response == null)
            {
                // A failed listing is not an empty dataset; let the caller report it as an internal error.
                throw new InvalidOperationException(
                    $"Listing bucket '{_settings.DatasetBucket}' with prefix '{prefix}' failed");
            }

            return response.S3Objects.Select(o => o.Key).ToList();
        }

        public async Task<Stream> OpenReadAsync(string key, CancellationToken ct)
        {
            // Buffer so the S3 response can be disposed here instead of leaking with the returned stream.
            using var response = await _helper.GetCopyObject(_settings.DatasetBucket, key);
            var buffer = new MemoryStream();
            await response.ResponseStream.CopyToAsync(buffer, ct);
            buffer.Position = 0;
            return buffer;
        }
    }
}
