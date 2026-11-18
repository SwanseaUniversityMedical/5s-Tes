using Agent.Api.Models;
using Agent.Api.Repositories.DbContexts;
using FiveSafesTes.Core.Models.Enums;
using FiveSafesTes.Core.Models.Settings;
using FiveSafesTes.Core.Services;
using Serilog;

namespace Agent.Api.Services
{
   
    public class AirlockEgressProvider : IEgressProvider
    {
        private readonly IAirlockClientHelper _airlockClient;
        private readonly IMinioTreHelper _minioTreHelper;
        private readonly ISubmissionHelper _subHelper;
        private readonly AirlockSettings _airlockSettings;
        private readonly ApplicationDbContext _dbContext;

        public AirlockEgressProvider(IAirlockClientHelper airlockClient, IMinioTreHelper minioTreHelper,
            ISubmissionHelper subHelper, AirlockSettings airlockSettings, ApplicationDbContext dbContext)
        {
            _airlockClient = airlockClient;
            _minioTreHelper = minioTreHelper;
            _subHelper = subHelper;
            _airlockSettings = airlockSettings;
            _dbContext = dbContext;
        }

        public async Task<EgressSendResult> SendForReviewAsync(EgressReviewRequest request)
        {
            AirlockEgressRequest? row = _dbContext.AirlockEgressRequests.FirstOrDefault(x => x.SubId == request.SubId);
            if (row == null)
            {
                row = new AirlockEgressRequest { SubId = request.SubId, State = AirlockEgressState.SendFailed };
                _dbContext.AirlockEgressRequests.Add(row);
            }

            row.TesId = request.TesId;
            row.TesName = request.TesName;
            row.ProjectId = request.ProjectId;
            row.OutputBucket = request.OutputBucket;

            return await SendAsync(row, request.Files);
        }

        
        public async Task<EgressSendResult> ResendAsync(AirlockEgressRequest row)
        {
            var listing = await _minioTreHelper.GetFilesInBucket(row.OutputBucket, $"{row.SubId}");
            var files = (listing?.S3Objects ?? new())
                .Select(o => new EgressOutputFile { Key = o.Key, Size = o.Size })
                .ToList();

            return await SendAsync(row, files);
        }

        private async Task<EgressSendResult> SendAsync(AirlockEgressRequest row, List<EgressOutputFile> files)
        {
            row.SendAttempts++;
            row.LastSentAt = DateTime.UtcNow;

            EgressSendResult result = await TrySendAsync(row, files);

            if (result.Success)
            {
                row.State = AirlockEgressState.Submitted;
                row.AirlockRequestId = result.ExternalRequestId;
                row.AirlockRequestVersion = result.ExternalRequestVersion;
                row.LastError = null;

                _subHelper.UpdateStatusForTre(row.SubId.ToString(), StatusType.DataOutApprovalBegun,
                    $"Airlock request {row.AirlockRequestId}");
            }
            else
            {
                row.State = AirlockEgressState.SendFailed;
                row.LastError = result.Error;
            }

            await _dbContext.SaveChangesAsync();
            return result;
        }

        private async Task<EgressSendResult> TrySendAsync(AirlockEgressRequest row, List<EgressOutputFile> files)
        {
            try
            {
                
                var skipped = files.Where(f => f.Size == 0).Select(f => f.Key).ToList();
                foreach (var key in skipped)
                {
                    Log.Warning("{Function} Not sending empty object {Key} for sub {SubId}", "AirlockEgressProvider", key, row.SubId);
                }

                var toSend = files.Where(f => f.Size > 0).ToList();
                if (toSend.Count == 0)
                {
                    return Fail(row, "No non-empty output files to send to Airlock.");
                }

                var uploads = new List<AirlockUploadFile>();
                var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var file in toSend)
                {
                    if (!AirlockFileNames.TryFromObjectKey(row.SubId, file.Key, out string name, out string? error))
                    {
                        return Fail(row, error!);
                    }

                    if (!names.Add(name))
                    {
                        return Fail(row, $"Output files map to the same Airlock file name \"{name}\".");
                    }

                    string key = file.Key;
                    string bucket = row.OutputBucket;
                    uploads.Add(new AirlockUploadFile
                    {
                        Filename = name,
                        OpenStream = async () => (await _minioTreHelper.GetCopyObject(bucket, key)).ResponseStream
                    });
                }

                var airlockRequest = new AirlockExternalRequest
                {
                    ExternalReference = row.SubId.ToString(),
                    ProjectId = row.ProjectId.ToString(),
                    RequestName = string.IsNullOrWhiteSpace(row.TesName)
                        ? $"5S-TES submission {row.SubId}"
                        : $"5S-TES submission {row.SubId} ({row.TesName})",
                    Plane = _airlockSettings.Plane,
                    Product = _airlockSettings.Product,
                    Metadata = new Dictionary<string, string>
                    {
                        ["source"] = "5S-TES",
                        ["subId"] = row.SubId.ToString(),
                        ["tesId"] = row.TesId,
                        ["tesName"] = row.TesName ?? ""
                    }
                };

                AirlockFileRequest created = await _airlockClient.SubmitExternalRequestAsync(airlockRequest, uploads);

                Log.Information("{Function} Sub {SubId} sent to Airlock as request {RequestId} v{Version} with {FileCount} files ({Skipped} empty skipped), attempt {Attempt}",
                    "AirlockEgressProvider", row.SubId, created.RequestId, created.Version, uploads.Count, skipped.Count, row.SendAttempts);

                return new EgressSendResult
                {
                    Success = true,
                    ExternalRequestId = created.RequestId,
                    ExternalRequestVersion = created.Version
                };
            }
            catch (Exception ex)
            {
                Log.Error(ex, "{Function} Sending sub {SubId} to Airlock failed (attempt {Attempt})", "AirlockEgressProvider", row.SubId, row.SendAttempts);
                return new EgressSendResult { Success = false, Error = ex.Message };
            }
        }

        private static EgressSendResult Fail(AirlockEgressRequest row, string error)
        {
            Log.Error("{Function} Not sending sub {SubId} to Airlock: {Error}", "AirlockEgressProvider", row.SubId, error);
            return new EgressSendResult { Success = false, Error = error };
        }
    }

    /// <summary>
    /// Maps TRE output object keys to Airlock file names. Airlock file names cannot contain path
    /// separators, so the "{subId}/" prefix is dropped and deeper folders become "__".
    /// The Airlock callback (AirlockOutcomeService) uses the same rule to match Airlock's per-file decisions to object keys.
    /// </summary>
    public static class AirlockFileNames
    {
        private const string FolderSeparator = "__";
        private const int MaxLength = 255;

        public static bool TryFromObjectKey(int subId, string key, out string name, out string? error)
        {
            string prefix = $"{subId}/";
            string relative = key.StartsWith(prefix, StringComparison.Ordinal) ? key.Substring(prefix.Length) : key;

            name = relative.Replace("/", FolderSeparator).Replace("\\", FolderSeparator).Replace(":", "_");

            // Same rules as Airlock's file-name check, so a bad name fails here with a clear message.
            if (string.IsNullOrWhiteSpace(name))
                error = $"Output \"{key}\" has no file name.";
            else if (name.Length > MaxLength)
                error = $"Output \"{key}\" gives a file name longer than {MaxLength} characters.";
            else if (name.Contains(".."))
                error = $"Output \"{key}\" contains \"..\", which Airlock does not accept.";
            else if (name != name.Trim() || name.StartsWith('.') || name.EndsWith('.'))
                error = $"Output \"{key}\" starts or ends with a dot or whitespace, which Airlock does not accept.";
            else
                error = null;

            return error == null;
        }
    }
}
