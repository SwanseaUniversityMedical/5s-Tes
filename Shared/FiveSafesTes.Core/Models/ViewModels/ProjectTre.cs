using Microsoft.AspNetCore.Mvc.Rendering;
using Newtonsoft.Json;

namespace FiveSafesTes.Core.Models.ViewModels
{
    public class ProjectTre
    {
        public int ProjectId { get; set; }
        public int TreId { get; set; }

        [JsonIgnore]
        public IEnumerable<SelectListItem>? ProjectItemList { get; set; }

        [JsonIgnore]
        public IEnumerable<SelectListItem>? TreItemList { get; set; }
    }
}
