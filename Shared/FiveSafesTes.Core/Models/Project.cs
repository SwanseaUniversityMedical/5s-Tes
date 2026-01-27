
namespace FiveSafesTes.Core.Models
{
    public class Project : BaseModel
    {
        public int Id { get; set; }
        
        public virtual List<User> Users { get; set; }

        public virtual List<Tre> Tres { get; set; }
        public string FormData { get; set; }
        public string Name { get; set; }
        public string Display { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }

        public string ProjectDescription { get; set; }

        public string? ProjectOwner { get; set; }
        public string? ProjectContact { get; set; }
        public bool MarkAsEmbargoed { get; set; }
        public string? SubmissionBucket { get; set; }
        public string? OutputBucket { get; set; }

        
        public virtual List<Submission> Submissions { get; set; }
        public virtual List<AuditLog>? AuditLogs { get; set; }

        public virtual List<ProjectTreDecision> ProjectTreDecisions { get; set; }
        public virtual List<MembershipTreDecision> MembershipTreDecision { get; set; }

    }



    public class ProjectListModel
    {
        public List<Project> Projects { get; set; }

    }
}
