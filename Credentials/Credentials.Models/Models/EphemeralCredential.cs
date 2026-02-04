using System.ComponentModel.DataAnnotations.Schema;

namespace Credentials.Models.Models
{
    [Table("EphemeralCredentials")]
    public class EphemeralCredential
    {
        public int Id { get; set; }
        public int SubmissionId { get; set; } //Get from submission

        public long? ParentProcessInstanceKey { get; set; } //To store the parent process instance key for Start Credentials
        public long? ProcessInstanceKey { get; set; } //Child process Instance key for each camunda flow 

        public string? CredentialType { get; set; }

        public string? VaultPath { get; set; } 
        public DateTime? CreatedAt { get; set; }
        public bool IsProcessed { get; set; } = false; //a flag to know if the creds are process in Agent.Api or not
        public string? ErrorMessage { get; set; }

        public DateTime? ExpiredAt { get; set; }

        public SuccessStatus? SuccessStatus { get; set; }
    }

    public enum SuccessStatus
    {
        Error = 0,
        Success = 1
    }
}
