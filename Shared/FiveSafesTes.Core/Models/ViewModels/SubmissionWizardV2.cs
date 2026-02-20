using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;

namespace FiveSafesTes.Core.Models.ViewModels
{
    public class SubmissionWizardV2
    {
        public int ProjectId { get; set; }

        [Display(Name = "TES Name")] public string? TESName { get; set; }

        [Display(Name = "TES Description")] public string? TESDescription { get; set; }

        [Display(Name = "Submitting to Project: ")]
        public string? ProjectName { get; set; }

        [Display(Name = "Select TREs")] public List<string>? Tres { get; set; }

        public List<TreInfo>? TreRadios { get; set; }

        public List<string>? SelectTresOptions { get; set; }

        [Display(Name = "Select TREs or leave blank for all")]
        public string? SelectedTres { get; set; }

        public List<ExecutorsV2>? Executors { get; set; }

        public string? DataInputType { get; set; } = "";

        public string? DataInputPath { get; set; } = "";
        
        public string? DataOutputType { get; set; } = "DIRECTORY";

        public string? DataOutputPath { get; set; } = "/outputs";
        
        public string? DataOutputUrl { get; set; } = "s3://";
        
        public string? DataOutputDescription { get; set; } = "Results from the requested query execution";
        
        public string? DataOutputName { get; set; } = "Query Results";

        public string? RawInput { get; set; }

        public string? Query { get; set; }

        public virtual List<Submission>? Submissions { get; set; }

        public IEnumerable<SelectListItem>? TreItemList { get; set; }

        public IEnumerable<SelectListItem>? UserItemList { get; set; }

        public string? JsonData { get; set; } = string.Empty;
    }

    public class ExecutorsV2
    {
        public string Image { get; set; } = string.Empty;
        public List<string> Command { get; set; } = new();
        public List<string> ENV { get; set; } = new();
    }
}
