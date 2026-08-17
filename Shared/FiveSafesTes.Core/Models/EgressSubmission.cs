
using FiveSafesTes.Core.Models.Enums;

namespace FiveSafesTes.Core.Models
{
    public class EgressSubmission
    {
        public int Id { get; set; }
        public string? SubmissionId { get; set; }
        public EgressStatus Status { get; set; }
        public string? OutputBucket { get; set; }

       

        public DateTime? Completed { get; set; }
        public string? Reviewer { get; set; }
        public virtual List<EgressFile> Files { get; set; }

        public string? tesId { get; set; }

        public string? Name { get; set; } 
        
    }
}
