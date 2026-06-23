namespace FiveSafesTes.Core.Models.ViewModels
{
    public class SubmissionList
    {
        public List<Project.ProjectSubmissionDto> SubmissionsDTO{ get; set; }
        public bool UseParent { get; set; }
    }
}
