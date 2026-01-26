using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FiveSafesTes.Core.Models.Enums;

namespace FiveSafesTes.Core.Models.ViewModels
{
    public class MembershipTreDecisionDTO
    {
        public int ProjectId { get; set; }
        public int UserId { get; set; }
        public Decision Decision { get; set; }
    }
}
