namespace FiveSafesTes.Core.Models;

public class ProjectTreEnvironmentVariables
{
    public int Id { get; set; }
    public int ProjectId { get; set; }
    public int TreId { get; set; }
    public string EnvJson { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class ProjectTreEnvironmentVariable 
{
    public string Name { get; set; }
    public string Description { get; set; }
}
