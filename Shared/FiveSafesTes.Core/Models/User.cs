namespace FiveSafesTes.Core.Models
{
    public class User : BaseModel
    {
        public int Id { get; set; }
        public string? FullName { get; set; }   
        public string Name { get; set; }
        public string Email { get; set; }
        public virtual List<Project> Projects { get; set; }
        public virtual List<Submission> Submissions { get; set; }
        public string FormData { get; set; }

        public string? Biography { get; set; }
        public string? Organisation {get;set;}
        public virtual List<MembershipTreDecision> MembershipTreDecision { get; set; }
        public virtual List<AuditLog>? AuditLogs { get; set; }
    }
}
