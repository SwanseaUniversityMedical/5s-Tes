using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FiveSafesTes.Core.Models.Enums;

namespace FiveSafesTes.Core.Models.ViewModels
{
    public class SubmissionDetails
    {
        public string SubId { get; set; }
        public StatusType StatusType { get; set; }
        public string? Description { get; set; }

        
    }
}
