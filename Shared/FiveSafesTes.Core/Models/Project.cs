
using FiveSafesTes.Core.Models.Enums;

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

        public class ProjectSummary
        {
            public int Id { get; set; }
            public string Name { get; set; }
            public DateTime StartDate { get; set; }
            public DateTime EndDate { get; set; }
            public string? ProjectDescription { get; set; }
            public int SubmissionCount { get; set; }
            public int UserCount { get; set; }
            public int TreCount { get; set; }
        }

        
        public class ProjectDetailsDto
        {
          public int Id { get; set; }
          public string Name { get; set; } = string.Empty;
          public DateTime StartDate { get; set; }
          public DateTime EndDate { get; set; }
          public string? ProjectDescription { get; set; }
          public string? ProjectOwner { get; set; }
          public string? ProjectContact { get; set; }
          public string? SubmissionBucket { get; set; }
          public string? OutputBucket { get; set; }

          public List<ProjectUserDto> Users { get; set; } = [];
          public List<ProjectUserDto> UsersNotInProject { get; set; } = [];
          public List<ProjectTreDto> Tres { get; set; } = [];
          public List<ProjectTreDto> TresNotInProject { get; set; } = [];
          public List<ProjectSubmissionDto> Submissions { get; set; } = [];
        }

        public class ProjectUserDto
        {
          public int Id { get; set; }
          public string? Name { get; set; }
          public string? FullName { get; set; }
        }

        public class ProjectTreDto
        {
          public int Id { get; set; }
          public string Name { get; set; } = string.Empty;
          public Decision Decision { get; set; }
        }

        public class ProjectSubmissionDto
        {
          public int Id { get; set; }
          public int? ParentId { get; set; }
          public bool HasParent { get; set; }
          public StatusType Status { get; set; }
          public DateTime StartTime { get; set; }
          public DateTime EndTime { get; set; }
          public string TesName { get; set; } = string.Empty;
          public string ProjectName { get; set; } = string.Empty;
          public string? SubmittedByName { get; set; }
        }
    }



    public class ProjectListModel
    {
        public List<Project> Projects { get; set; }

    }
}
