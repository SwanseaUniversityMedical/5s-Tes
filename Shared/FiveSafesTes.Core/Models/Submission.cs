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
            var date = StartTime.ToString("yyyy/MM/dd:HH:mm:ss");
            return date;
        }

        public string GetCurrentStatusDisplayTime()
        {
            var end = EndTime == DateTime.MinValue ? DateTime.Now.ToUniversalTime() : EndTime;
           
            return TimeHelper.GetDisplayTime(LastStatusUpdate, end);
        }



    }

  
}

