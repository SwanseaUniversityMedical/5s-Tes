

namespace FiveSafesTes.Core.Models.ViewModels
{
    /// <summary>
    /// FormData is a repository for the JSON data submitted from a FormIo Form.
    /// </summary>
    public class FormData
    {
        public int Id { get; set; }
        public string? FormIoUrl { get; set; }
        public string? FormIoString { get; set; }
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
               
    }
}