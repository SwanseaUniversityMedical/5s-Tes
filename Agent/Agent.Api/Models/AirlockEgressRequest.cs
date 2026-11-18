using Microsoft.EntityFrameworkCore;

namespace Agent.Api.Models
{
    public enum AirlockEgressState
    {      
        SendFailed = 0,
        
        Submitted = 1,

        Closed = 2
    }

   
    /// Tracks one submission's outputs sent to Director-Airlock, so a failed send can be retried and
    /// Airlock's review-completion callback can be matched to its submission. One row per submission.
    
    [Index(nameof(SubId), IsUnique = true)]
    [Index(nameof(State))]
    [Index(nameof(AirlockRequestId))]
    public class AirlockEgressRequest
    {
        public int Id { get; set; }

        public int SubId { get; set; }
        public string TesId { get; set; } = string.Empty;
        public string? TesName { get; set; }
        public int ProjectId { get; set; }
       
        public string OutputBucket { get; set; } = string.Empty;

        public AirlockEgressState State { get; set; }
       
        public int? AirlockRequestId { get; set; }
        public int? AirlockRequestVersion { get; set; }

        // Airlock's final status, from its review-completion callback.
        public AirlockFileStatus? LastAirlockStatus { get; set; }

        public string? LastError { get; set; }
        public int SendAttempts { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? LastSentAt { get; set; }
        public DateTime? ClosedAt { get; set; }
    }
}
