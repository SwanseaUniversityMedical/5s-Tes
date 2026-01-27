using Microsoft.AspNetCore.Mvc.Rendering;
using Newtonsoft.Json;

namespace FiveSafesTes.Core.Models.ViewModels
{
    public class ProjectUser
    {
        public int UserId { get; set; }
        public int ProjectId { get; set; }
        [JsonIgnore]
        public IEnumerable<SelectListItem>? ProjectItemList { get; set; }
        [JsonIgnore]
        public IEnumerable<SelectListItem>? UserItemList { get; set; }
    }
}
