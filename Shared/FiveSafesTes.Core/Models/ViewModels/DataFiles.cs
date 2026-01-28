using FiveSafesTes.Core.Models.Enums;

namespace FiveSafesTes.Core.Models.ViewModels
{
    public class DataFiles: BaseModel
    {
        public int Id { get; set; }
        public int? SubmissionId { get; set; }
        public string? Name { get; set; }
        public string? TreBucketFullPath { get; set; }
        public string? SubmisionBucketFullPath { get; set; }
        public FileStatus Status { get; set; }
        public string? Description { get; set; }
        public DateTime? LastUpdate { get; set; }
        public string? Reviewer { get; set; }

    }
    public class DataList
    {
        public List<DataFiles>? Data { get; set; }

    }

}
