using FiveSafesTes.Core.Models;
using Submission.Api.Repositories.DbContexts;

namespace Submission.Api.Services
{
    public interface IProvenanceRecorder
    {
        void Record(
            string submissionId,
            string? tesTaskId,
            ProvenanceEventType eventType,
            string serviceName,
            string status,
            string? outcome = null,
            string? traceId = null,
            string? spanId = null,
            int? treId = null,
            int? projectId = null,
            string? actorType = null,
            string? actorIdHash = null,
            string? credentialIdHash = null,
            string? objectBucket = null,
            string? objectKeyHash = null,
            string? objectChecksum = null,
            string? sqlStatementHash = null,
            string? sqlTemplate = null,
            string? tableNames = null,
            int? rowsAffected = null,
            string? errorCode = null,
            string? errorSummarySafe = null,
            string? approvalDecision = null,
            string? transferDestinationHash = null);
    }

    public class ProvenanceRecorder : IProvenanceRecorder
    {
        private readonly ApplicationDbContext _dbContext;

        public ProvenanceRecorder(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public void Record(
            string submissionId,
            string? tesTaskId,
            ProvenanceEventType eventType,
            string serviceName,
            string status,
            string? outcome = null,
            string? traceId = null,
            string? spanId = null,
            int? treId = null,
            int? projectId = null,
            string? actorType = null,
            string? actorIdHash = null,
            string? credentialIdHash = null,
            string? objectBucket = null,
            string? objectKeyHash = null,
            string? objectChecksum = null,
            string? sqlStatementHash = null,
            string? sqlTemplate = null,
            string? tableNames = null,
            int? rowsAffected = null,
            string? errorCode = null,
            string? errorSummarySafe = null,
            string? approvalDecision = null,
            string? transferDestinationHash = null)
        {
            _dbContext.ProvenanceEvents.Add(new ProvenanceEvent
            {
                SubmissionId = submissionId,
                TesTaskId = tesTaskId,
                EventType = eventType,
                ServiceName = serviceName,
                Status = status,
                Outcome = outcome,
                TraceId = traceId,
                SpanId = spanId,
                TreId = treId,
                ProjectId = projectId,
                ActorType = actorType,
                ActorIdHash = actorIdHash,
                CredentialIdHash = credentialIdHash,
                ObjectBucket = objectBucket,
                ObjectKeyHash = objectKeyHash,
                ObjectChecksum = objectChecksum,
                SqlStatementHash = sqlStatementHash,
                SqlTemplate = sqlTemplate,
                TableNames = tableNames,
                RowsAffected = rowsAffected,
                ErrorCode = errorCode,
                ErrorSummarySafe = errorSummarySafe,
                ApprovalDecision = approvalDecision,
                TransferDestinationHash = transferDestinationHash,
                EventTimeUtc = DateTime.UtcNow,
                CreatedAtUtc = DateTime.UtcNow,
            });

            _dbContext.SaveChanges();
        }
    }
}
