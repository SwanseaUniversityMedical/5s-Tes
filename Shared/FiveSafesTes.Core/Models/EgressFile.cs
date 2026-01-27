using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FiveSafesTes.Core.Models.Enums;

namespace FiveSafesTes.Core.Models
{
    public class EgressFile
    {
        public int Id { get; set; }
        
        public string? Name { get; set; }
       
        public FileStatus Status { get; set; }
       
        public DateTime? LastUpdate { get; set; }
        public string? Reviewer { get; set; }

        public virtual EgressSubmission? EgressSubmission { get; set; }
    }
}
