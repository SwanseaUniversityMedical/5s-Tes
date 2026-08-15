namespace FiveSafesTes.Core.Models
{
    public class ProvenanceManifest
    {
        public string SubmissionId { get; set; } = string.Empty;
        public string? TesTaskId { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime StartTimeUtc { get; set; }
        public DateTime EndTimeUtc { get; set; } = DateTime.UtcNow;
        public bool CredentialsIssued { get; set; }
        public bool CredentialsRevoked { get; set; }
        public string? CredentialIdHash { get; set; }
        public int DatabaseQueriesLogged { get; set; }
        public List<string> TablesTouched { get; set; } = new();
        public int MinioObjectsTouched { get; set; }
        public List<string> OutputFiles { get; set; } = new();
        public string? ReviewDecision { get; set; }
        public string? TransferDestinationHash { get; set; }
        public string UserSafeSummary { get; set; } = string.Empty;
    }
}
