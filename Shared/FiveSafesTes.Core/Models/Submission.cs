using System.ComponentModel.DataAnnotations.Schema;
using FiveSafesTes.Core.Models.Enums;
using FiveSafesTes.Core.Models.Helpers;
using FiveSafesTes.Core.Models.ViewModels;

namespace FiveSafesTes.Core.Models
{
    public class Submission : BaseModel
    {
        public int Id { get; set; }
        public int? ParentId { get; set; }
        public string? TesId { get; set; }
        public string SourceCrate { get; set; }
        public string TesName { get; set; }
        public string? TesJson { get; set; }

        public string? FinalOutputFile { get; set; }
        public string DockerInputLocation { get; set; }
        public virtual Project Project { get; set; }
        [ForeignKey("ParentID")]
        public virtual Submission? Parent { get; set; }
        public virtual List<Submission> Children { get; set; }
        public virtual List<HistoricStatus> HistoricStatuses { get; set; }
        [NotMapped]
        public virtual List<StageInfo> StageInfo { get; set; }
        public virtual List<SubmissionFile> SubmissionFiles { get; set; }
        public virtual List<AuditLog>? AuditLogs { get; set; }
        public virtual Tre? Tre { get; set; }
        public virtual User SubmittedBy { get; set; }
        public DateTime LastStatusUpdate { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public StatusType Status { get; set; }
        public string? StatusDescription { get; set; }

        public string? QueryToken { get; set; }

        public string GetTotalDisplayTime()
        {
            var end = EndTime == DateTime.MinValue ? (DateTime.Now).ToUniversalTime() : EndTime;
            var data = TimeHelper.GetDisplayTime(StartTime, end);
            return data;
        }

        public string GetFormattedStartDate()
        {
            var date = StartTime.ToString("yyyy/MM/dd HH:mm:ss");
            return date;
        }
        
        
        public class SubmissionSummary
        {
          public int Id { get; set; }
          public int? ParentId { get; set; }
          public string TesName { get; set; }
          public StatusType Status { get; set; }
          public DateTime StartTime { get; set; }
          public DateTime EndTime { get; set; }
          public string? ProjectName { get; set; }
          public string? ProjectOutputBucket { get; set; }
          public string? SubmittedByName { get; set; }
          public string? SubmittedByFullName { get; set; }
        
        }

        public class SubmissionDetailsDto
        {
            public int Id { get; set; }
            public string? TesId { get; set; }
            public string TesName { get; set; } = string.Empty;
            public string? TesJson { get; set; }
            public StatusType Status { get; set; }
            public DateTime LastStatusUpdate { get; set; }
            public DateTime StartTime { get; set; }
            public DateTime EndTime { get; set; }
            public ProjectLinkDto Project { get; set; } = new();
            public UserLinkDto SubmittedBy { get; set; } = new();
            public List<SubmissionChildDto> Children { get; set; } = [];

            public string GetTotalDisplayTime()
            {
                var end = EndTime == DateTime.MinValue ? DateTime.Now.ToUniversalTime() : EndTime;
                return TimeHelper.GetDisplayTime(StartTime, end);
            }
        }

        public class SubmissionChildDto
        {
            public int Id { get; set; }
            public StatusType Status { get; set; }
            public DateTime LastStatusUpdate { get; set; }
            public DateTime StartTime { get; set; }
            public DateTime EndTime { get; set; }
            public TreLinkDto? Tre { get; set; }
            public List<SubmissionHistoricStatusDto> HistoricStatuses { get; set; } = [];

            public string GetTotalDisplayTime()
            {
                var end = EndTime == DateTime.MinValue ? DateTime.Now.ToUniversalTime() : EndTime;
                return TimeHelper.GetDisplayTime(StartTime, end);
            }
        }

        public class SubmissionHistoricStatusDto
        {
            public DateTime Start { get; set; }
            public DateTime End { get; set; }
            public StatusType Status { get; set; }
            public bool IsCurrent { get; set; }
            public bool IsStillRunning { get; set; }

            public string GetDisplayRunTime()
            {
                return TimeHelper.GetDisplayTime(Start, End);
            }
        }

        public class ProjectLinkDto
        {
            public int Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public string? SubmissionBucket { get; set; }
            public string? OutputBucket { get; set; }
        }

        public class UserLinkDto
        {
            public int Id { get; set; }
            public string? Name { get; set; }
            public string? FullName { get; set; }
        }

        public class TreLinkDto
        {
            public int Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public DateTime LastHeartBeatReceived { get; set; }

            public bool IsOnline()
            {
                return (DateTime.UtcNow - LastHeartBeatReceived).TotalMinutes < 30;
            }
        }


    }

  
}

