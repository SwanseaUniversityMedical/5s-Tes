
namespace FiveSafesTes.Core.Models
{
    public class TreAuditLog : BaseModel
    {
        public int Id { get; set; }
        public bool Approved { get; set; }
        public string? ApprovedBy { get; set; }
        public string? IPaddress { get; set; }
        public DateTime Date { get; set; }

        public virtual TreProject? Project { get; set; }
        public virtual TreMembershipDecision? MembershipDecision { get; set; }

    }

}
