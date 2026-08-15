using FiveSafesTes.Core.Models;
using Microsoft.EntityFrameworkCore;
using Submission.Api.Repositories.DbContexts;

namespace Submission.Api.Services
{
    public interface IProvenanceManifestService
    {
        ProvenanceManifest BuildManifest(string submissionId);
    }

    public class ProvenanceManifestService : IProvenanceManifestService
    {
        private readonly ApplicationDbContext _dbContext;

        public ProvenanceManifestService(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public ProvenanceManifest BuildManifest(string submissionId)
        {
            var events = _dbContext.ProvenanceEvents
                .Where(x => x.SubmissionId == submissionId)
                .OrderBy(x => x.EventTimeUtc)
                .ToList();

            var manifest = new ProvenanceManifest
            {
                SubmissionId = submissionId,
                TesTaskId = events.Select(x => x.TesTaskId).FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)),
                Status = events.OrderByDescending(x => x.EventTimeUtc).FirstOrDefault()?.Status ?? "unknown",
                StartTimeUtc = events.Any() ? events.Min(x => x.EventTimeUtc) : DateTime.UtcNow,
                EndTimeUtc = events.Any() ? events.Max(x => x.EventTimeUtc) : DateTime.UtcNow,
                CredentialsIssued = events.Any(x => x.EventType == ProvenanceEventType.CredentialsIssued),
                CredentialsRevoked = events.Any(x => x.EventType == ProvenanceEventType.CredentialsRevoked),
                CredentialIdHash = events.Select(x => x.CredentialIdHash).FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)),
                DatabaseQueriesLogged = events.Count(x => x.EventType == ProvenanceEventType.SqlExecuted),
                TablesTouched = events
                    .Where(x => !string.IsNullOrWhiteSpace(x.TableNames))
                    .SelectMany(x => x.TableNames.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                    .Distinct()
                    .ToList(),
                MinioObjectsTouched = events.Count(x => x.EventType == ProvenanceEventType.MinioObjectWritten || x.EventType == ProvenanceEventType.MinioObjectRead || x.EventType == ProvenanceEventType.MinioObjectDeleted),
                OutputFiles = events
                    .Where(x => x.EventType == ProvenanceEventType.MinioObjectWritten)
                    .Select(x => x.ObjectKeyHash ?? "unknown")
                    .Distinct()
                    .ToList(),
                ReviewDecision = events.OrderByDescending(x => x.EventTimeUtc)
                    .Select(x => x.ApprovalDecision)
                    .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)),
                TransferDestinationHash = events.OrderByDescending(x => x.EventTimeUtc)
                    .Select(x => x.TransferDestinationHash)
                    .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x))
            };

            manifest.UserSafeSummary = BuildUserSafeSummary(manifest);
            return manifest;
        }

        private static string BuildUserSafeSummary(ProvenanceManifest manifest)
        {
            var parts = new List<string>
            {
                $"Submission {manifest.SubmissionId} completed with status {manifest.Status}.",
                $"The job ran from {manifest.StartTimeUtc:O} to {manifest.EndTimeUtc:O}.",
                $"Credentials were {(manifest.CredentialsIssued ? "issued" : "not issued")} and {(manifest.CredentialsRevoked ? "revoked" : "not revoked")}.",
                $"The workload recorded {manifest.DatabaseQueriesLogged} database operations and touched {manifest.MinioObjectsTouched} data objects.",
                $"The review decision was {(string.IsNullOrWhiteSpace(manifest.ReviewDecision) ? "not recorded" : manifest.ReviewDecision)}.",
                $"The output result is represented by {manifest.OutputFiles.Count} hashed object references."
            };

            return string.Join(" ", parts);
        }
    }
}
