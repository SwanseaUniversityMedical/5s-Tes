using Agent.Api.Models;
using Agent.Api.Repositories.DbContexts;
using FiveSafesTes.Core.Models.Enums;
using FiveSafesTes.Core.Models.ViewModels;
using FiveSafesTes.Core.Services;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Serilog;

namespace Agent.Api.Services
{
    public enum AirlockOutcomeResult
    {
        Applied,
        AlreadyApplied,
        UnknownRequest
    }

    public interface IAirlockOutcomeService
    {
        /// <summary>
        /// Applies a finished Airlock review sent by Airlock's completion callback (the review DMN's
        /// APIsCallOnCompletion): updates the submission and copies the approved files, as the Data
        /// Egress EgressResults callback does. Safe to call more than once for the same request.
        /// </summary>
        Task<AirlockOutcomeResult> ApplyAsync(AirlockReviewOutcome outcome);
    }

    public class AirlockOutcomeService : IAirlockOutcomeService
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly IEgressOutcomeService _egressOutcomeService;
        private readonly ISubmissionHelper _subHelper;
        private readonly IMinioTreHelper _minioTreHelper;

        public AirlockOutcomeService(ApplicationDbContext dbContext, IEgressOutcomeService egressOutcomeService,
            ISubmissionHelper subHelper, IMinioTreHelper minioTreHelper)
        {
            _dbContext = dbContext;
            _egressOutcomeService = egressOutcomeService;
            _subHelper = subHelper;
            _minioTreHelper = minioTreHelper;
        }

        public async Task<AirlockOutcomeResult> ApplyAsync(AirlockReviewOutcome outcome)
        {
            AirlockEgressRequest? row = FindRow(outcome);
            if (row == null)
            {
                Log.Warning("{Function} No 5S-TES submission for Airlock request {RequestId} v{Version}",
                    "AirlockOutcomeService", outcome.RequestId, outcome.Version);
                return AirlockOutcomeResult.UnknownRequest;
            }

            if (row.State == AirlockEgressState.Closed)
            {
                Log.Information("{Function} Sub {SubId}: outcome for Airlock request {RequestId} already applied, ignoring",
                    "AirlockOutcomeService", row.SubId, outcome.RequestId);
                return AirlockOutcomeResult.AlreadyApplied;
            }

            row.AirlockRequestId ??= outcome.RequestId;
            row.AirlockRequestVersion ??= outcome.Version;
            row.LastAirlockStatus = outcome.Status;
            row.LastError = null;

            if (outcome.Status is AirlockFileStatus.Withdrawn or AirlockFileStatus.Revoked or AirlockFileStatus.Removed)
            {
                string reason = $"Airlock request {outcome.RequestId} was {outcome.Status.ToString().ToLowerInvariant()}.";
                Log.Warning("{Function} Sub {SubId}: {Reason}", "AirlockOutcomeService", row.SubId, reason);
                _subHelper.UpdateStatusForTre(row.SubId.ToString(), StatusType.DataOutApprovalRejected, reason);
                _subHelper.CloseSubmissionForTre(row.SubId.ToString(), StatusType.Failed, reason, "");
                Close(row);
                await _dbContext.SaveChangesAsync();
                return AirlockOutcomeResult.Applied;
            }

            List<EgressResult> results = await MapDecisions(row, outcome);
            if (results.Count == 0)
            {
                string reason = $"Airlock request {outcome.RequestId} finished ({outcome.OutcomeStatus}) but none of its files match the outputs.";
                Log.Error("{Function} Sub {SubId}: {Reason}", "AirlockOutcomeService", row.SubId, reason);
                _subHelper.CloseSubmissionForTre(row.SubId.ToString(), StatusType.Failed, reason, "");
                row.LastError = reason;
                Close(row);
                await _dbContext.SaveChangesAsync();
                return AirlockOutcomeResult.Applied;
            }

            Log.Information("{Function} Sub {SubId}: Airlock request {RequestId} finished ({Outcome}), {Approved}/{Total} files approved",
                "AirlockOutcomeService", row.SubId, outcome.RequestId, outcome.OutcomeStatus, results.Count(r => r.Approved), results.Count);

            await _egressOutcomeService.ApplyOutcomeAsync(new EgressReview
            {
                SubId = row.SubId.ToString(),
                OutputBucket = row.OutputBucket,
                FileResults = results
            });

            Close(row);
            await _dbContext.SaveChangesAsync();
            return AirlockOutcomeResult.Applied;
        }

        /// <summary>
        /// Finds the submission by Airlock request id. Falls back to the subId 5S-TES put in the request's
        /// metadata, for a send that reached Airlock but whose reply never came back (so the id wasn't saved).
        /// </summary>
        private AirlockEgressRequest? FindRow(AirlockReviewOutcome outcome)
        {
            AirlockEgressRequest? row = _dbContext.AirlockEgressRequests
                .FirstOrDefault(x => x.AirlockRequestId == outcome.RequestId);
            if (row != null) return row;

            string? subId = ReadMetadataValue(outcome.Metadata, "subId");
            return int.TryParse(subId, out int id)
                ? _dbContext.AirlockEgressRequests.FirstOrDefault(x => x.SubId == id && x.AirlockRequestId == null)
                : null;
        }

        /// <summary>
        /// Matches each Airlock file back to its object key with the naming rule used when sending.
        /// A file counts as approved only if it is in Airlock's ApprovedFiles list (approved by every reviewer).
        /// </summary>
        private async Task<List<EgressResult>> MapDecisions(AirlockEgressRequest row, AirlockReviewOutcome outcome)
        {
            var listing = await _minioTreHelper.GetFilesInBucket(row.OutputBucket, $"{row.SubId}");
            var keyByName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var obj in listing?.S3Objects ?? new())
            {
                if (AirlockFileNames.TryFromObjectKey(row.SubId, obj.Key, out string name, out _))
                {
                    keyByName.TryAdd(name, obj.Key);
                }
            }

            var approved = ReadApprovedFileNames(outcome.ApprovedFiles);

            var results = new List<EgressResult>();
            foreach (var file in outcome.RequestObject?.Files ?? new())
            {
                if (file.Filename != null && keyByName.TryGetValue(file.Filename, out string? key))
                {
                    results.Add(new EgressResult { FileName = key, Approved = approved.Contains(file.Filename) });
                }
                else
                {
                    Log.Warning("{Function} Sub {SubId}: Airlock file {Filename} has no matching output object",
                        "AirlockOutcomeService", row.SubId, file.Filename);
                }
            }

            return results;
        }

        private static HashSet<string> ReadApprovedFileNames(string? approvedFilesJson)
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(approvedFilesJson)) return names;

            foreach (var item in JArray.Parse(approvedFilesJson))
            {
                string? name = item["FileName"]?.Value<string>();
                if (!string.IsNullOrEmpty(name)) names.Add(name);
            }

            return names;
        }

        private static string? ReadMetadataValue(string? metadataJson, string key)
        {
            if (string.IsNullOrWhiteSpace(metadataJson)) return null;
            try
            {
                return JObject.Parse(metadataJson)[key]?.Value<string>();
            }
            catch (JsonException)
            {
                return null;
            }
        }

        private static void Close(AirlockEgressRequest row)
        {
            row.State = AirlockEgressState.Closed;
            row.ClosedAt = DateTime.UtcNow;
        }
    }
}
