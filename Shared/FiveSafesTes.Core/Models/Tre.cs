using FiveSafesTes.Core.Models.Helpers;

namespace FiveSafesTes.Core.Models
{
    public class Tre: BaseModel
    {
        public int Id { get; set; }        
        public virtual List<Project> Projects { get; set; }
        public string Name { get; set; }

        public DateTime LastHeartBeatReceived { get; set; }
        public string AdminUsername { get; set; }

        public string About {  get; set; }
        public string FormData { get; set; }
        public virtual List<Submission> Submissions { get; set; }

        public virtual List<ProjectTreDecision> ProjectTreDecisions { get; set; }

        public virtual List<MembershipTreDecision> MembershipTreDecision { get; set; }
        public virtual List<AuditLog>? AuditLogs { get; set; }


        public bool IsOnline()
        {
            TimeSpan timeSinceLastUpdate = DateTime.UtcNow - LastHeartBeatReceived;
            var isOnline = false;
            if (timeSinceLastUpdate.TotalMinutes < 30)
            {
                isOnline = true;
            }
            return isOnline;
                
        }

        public string GetTotalDisplayTime()
        {
            var end = (DateTime.Now).ToUniversalTime();
            var data = TimeHelper.GetDisplayTime(LastHeartBeatReceived, end);
            return data;
        }

    }
}
