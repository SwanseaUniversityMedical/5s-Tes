using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
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
        
        public string GetMinioBucketUrl(string minioBaseUrl = "http://localhost:9003")
        {
            return string.IsNullOrEmpty(OutputBucket) 
                ? "#" 
                : $"{minioBaseUrl}/browser/{OutputBucket}";
        }
        
        public string EgressStatusDisplay
        {
            get
            {
                var enumType = typeof(EgressStatus);
                var memberInfo = enumType.GetMember(Status.ToString());
                var displayAttribute = memberInfo.FirstOrDefault()?.GetCustomAttribute<DisplayAttribute>();

                return displayAttribute?.Name ?? Status.ToString();
            }
        }

        public string EgressID()
        {
            if (string.IsNullOrEmpty(tesId))
            {
                return SubmissionId;
            }
            else
            {
                return tesId;
            }
        }
    }
}
