using System.ComponentModel.DataAnnotations;
using System.Security.AccessControl;
using System.Xml.Linq;
using FiveSafesTes.Core.Models.Enums;

namespace FiveSafesTes.Core.Models
{
    public class TreMembershipDecision : BaseModel
    {
        public int Id { get; set; }
        public virtual TreUser? User { get; set; }
        public virtual TreProject? Project { get; set; }
        public bool Archived { get; set; }
        public Decision Decision { get; set; }
        [Display(Name = "Project Expiry Date")]
        public DateTime ProjectExpiryDate { get; set; }

        [Display(Name = "Approved By")]
        public string? ApprovedBy { get; set; }

        [Display(Name = "Date of Last Decision")]
        public DateTime LastDecisionDate { get; set; }

        public virtual List<TreAuditLog>? AuditLogs { get; set; }
    }
    
}
