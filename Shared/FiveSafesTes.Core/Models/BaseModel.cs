using System.ComponentModel.DataAnnotations.Schema;

namespace FiveSafesTes.Core.Models
{
    public class BaseModel
    {
        [NotMapped]
        public bool Error { get; set; }
        [NotMapped]

        public string? ErrorMessage { get; set; }
    }

    
}
