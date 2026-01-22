using FiveSafesTes.Core.Models.Enums;
using System.ComponentModel.DataAnnotations;
using System.Xml.Linq;

namespace FiveSafesTes.Core.Models
{
    public class TreUser : BaseModel
    {
        public int Id { get; set; }
        [Display(Name = "Submission Id")]
        public int SubmissionUserId { get; set; }

        [Display(Name = "Membership Decisions")]
        public virtual List<TreMembershipDecision> MemberDecisions { get; set; }

        public bool Archived { get; set; }

        public string? Username { get; set; }
        public string? Email { get; set; }



    }
    
}
