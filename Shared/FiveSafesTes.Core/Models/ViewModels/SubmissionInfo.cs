using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FiveSafesTes.Core.Models.ViewModels
{
    public class SubmissionInfo
    {
        
        public string GetALlIDs()
        {
            return $"Submission ID: {Submission.Id.ToString()}";
        }

        public Submission Submission { get; set; }
        public Stages Stages { get; set; }


        
    }
}
