
namespace FiveSafesTes.Core.Models
{
    public class AuditLog : BaseModel
    {
        public int Id { get; set; }

        public virtual Project? Project { get; set; }
        public virtual User? User { get; set; }
        public virtual Tre? Tre { get; set; }
        public virtual Submission? Submission { get; set; }
       
        public string? LoggedInUserName { get; set; }
        public string? HistoricFormData { get; set; }
        public string? IPaddress { get; set; }

        public LogType LogType { get; set; }
        public DateTime Date { get; set; }

    }

    public enum LogType
    {
        AddProject = 0,
        UpdateProject = 1,
        AddUser = 2,
        UpdateUser = 3,
        AddTre = 4,
        UpdateTre = 5,
        AddTreToProject = 6,
        RemoveTreFromProject = 7,
        AddUserToProject = 8,
        RemoveUserFromProject = 9,
        CreateSubmission = 10
        

    }

}
