using System.ComponentModel.DataAnnotations;

namespace FiveSafesTes.Core.Models
{
    public class ProvenanceEvent
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        public string SubmissionId { get; set; } = string.Empty;
        public string? TesTaskId { get; set; }

        public string? TraceId { get; set; }
        public string? SpanId { get; set; }

        public int? TreId { get; set; }
        public int? ProjectId { get; set; }

        public ProvenanceEventType EventType { get; set; }
        public string ServiceName { get; set; } = string.Empty;

        public string? ActorType { get; set; }
        public string? ActorIdHash { get; set; }
        public string? CredentialIdHash { get; set; }

        public string? ObjectBucket { get; set; }
        public string? ObjectKeyHash { get; set; }
        public string? ObjectChecksum { get; set; }

        public string? SqlStatementHash { get; set; }
        public string? SqlTemplate { get; set; }
        public string? TableNames { get; set; }
        public int? RowsAffected { get; set; }

        public string Status { get; set; } = string.Empty;
        public string? Outcome { get; set; }
        public string? ErrorCode { get; set; }
        public string? ErrorSummarySafe { get; set; }

        public string? ApprovalDecision { get; set; }
        public string? TransferDestinationHash { get; set; }

        public DateTime EventTimeUtc { get; set; } = DateTime.UtcNow;
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    }

    public enum ProvenanceEventType
    {
        SubmissionReceived,
        SubmissionValidated,
        CredentialsIssued,
        CredentialsRevoked,
        TesSubmitted,
        TesQueued,
        TesRunning,
        TesCompleted,
        TesFailed,
        SqlExecuted,
        MinioObjectWritten,
        MinioObjectRead,
        MinioObjectDeleted,
        OutputReviewed,
        OutputApproved,
        OutputRejected,
        EgressStarted,
        EgressCompleted,
        EgressFailed,
        Failure
    }
}
