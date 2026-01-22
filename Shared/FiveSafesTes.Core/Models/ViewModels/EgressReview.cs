using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FiveSafesTes.Core.Models.ViewModels
{
    public class EgressReview
    {
        public string SubId { get; set; }
        public List<EgressResult> FileResults { get; set; }

        public string OutputBucket { get; set; }
    }

    public class EgressResult
    {
        public string? FileName { get; set; }
        public bool Approved { get; set; } 
    }
}
