using System.ComponentModel.DataAnnotations.Schema;
using FiveSafesTes.Core.Models.Enums;
using FiveSafesTes.Core.Models.Helpers;

namespace FiveSafesTes.Core.Models
{
    public class HistoricStatus : BaseModel
    {
        
        public int Id { get; set; }
        public DateTime Start { get; set; }
        public DateTime End { get; set; }
        public virtual Submission Submission { get; set; }
        public StatusType Status { get; set; }
        public string? StatusDescription { get; set; }

        [NotMapped]
        public bool IsCurrent { get; set; }
        [NotMapped]
        public bool IsStillRunning { get; set; }


        public string GetDisplayRunTime()
        {

            return TimeHelper.GetDisplayTime(Start, End);

        }

    }

    
}
