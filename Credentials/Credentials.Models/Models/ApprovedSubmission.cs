using System.ComponentModel.DataAnnotations.Schema;

namespace Credentials.Models.Models;

[Table("ApprovedSubmissions")]
public class ApprovedSubmission
{
    public int Id { get; set; }
    public int SubmissionId { get; set; }
    public string Project {  get; set; }
    public int UserId { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsProcessed { get; set; } = false;
    public DateTime ProcessedAt { get; set; }
}
