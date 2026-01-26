using System.ComponentModel.DataAnnotations;
using System.Xml.Linq;
using FiveSafesTes.Core.Models.Enums;

namespace FiveSafesTes.Core.Models
{
    public class TreProject : BaseModel
    {
        public int Id { get; set; }
        [Display(Name = "Submission Id")]
        public int SubmissionProjectId { get; set; }
        [Display(Name = "Username")]
        public string? UserName { get; set; }
        public string? Password { get; set; }

        [Display(Name = "Submission Name")]
        public string? SubmissionProjectName { get; set; }
        public string? Description { get; set; }

        [Display(Name = "Membership Decisions")]
        public virtual List<TreMembershipDecision>? MemberDecisions { get; set; }

        [Display(Name = "Local Name")]
        public string? LocalProjectName { get; set; }
        public Decision Decision { get; set; }
        public bool Archived { get; set; }

        [Display(Name = "Approved By")]
        public string? ApprovedBy { get; set; }

        [Display(Name = "Date of Last Decision")]
        public DateTime LastDecisionDate { get; set; }

        [Display(Name = "Project Expiry Date")]
        public DateTime ProjectExpiryDate { get; set; }

        [Display(Name = "Submission Bucket for TRE Layer")]
        public string? SubmissionBucketTre { get; set; }
		
        [Display(Name = "Output Bucket Out for TRE Layer")]
        public string? OutputBucketTre { get; set; }

        public virtual List<TreAuditLog>? AuditLogs { get; set; }

        public string? OutputBucketSub { get; set; }
    }
    
}
