using Microsoft.AspNetCore.Mvc.Rendering;
using System.Text.Json.Serialization;


namespace FiveSafesTes.Core.Models.ViewModels
{
    public class ProjectUserTre
    {
        public int Id { get; set; }
        public virtual List<UserGetProjectModel> Users { get; set; }
        public virtual List<TreGetProjectModel> Tres { get; set; }
        public string FormData { get; set; }
        public string FormIoUrl { get; set; }
        public string Name { get; set; }
        public string? ProjectOwner { get; set; }
        public string? ProjectContact { get; set; }
        public string ProjectDescription { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string? SubmissionBucket { get; set; }
        public string? OutputBucket { get; set; }
        public string? MinioEndpoint { get; set; }

        [JsonIgnore]
        public virtual List<SubmissionsGetProjectModel> Submissions { get; set; }

        [JsonIgnore]
        public IEnumerable<SelectListItem>? TreItemList { get; set; }

        [JsonIgnore]
        public IEnumerable<SelectListItem>? UserItemList { get; set; }
    }
}
