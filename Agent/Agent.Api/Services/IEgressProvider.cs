namespace Agent.Api.Services
{
    
    // Sends a finished TES task's output files for egress review. Which system does the review
    // (Data Egress or Director-Airlock) is chosen by EgressSettings:Provider.
   
    public interface IEgressProvider
    {
        Task<EgressSendResult> SendForReviewAsync(EgressReviewRequest request);
    }

    public class EgressReviewRequest
    {
        public int SubId { get; set; }
        public string TesId { get; set; } = string.Empty;
        public string? TesName { get; set; }

        // Submission-layer project id, as passed to CheckTES.
        public int ProjectId { get; set; }

        public string OutputBucket { get; set; } = string.Empty;
        public List<EgressOutputFile> Files { get; set; } = new();
    }

    public class EgressOutputFile
    {
        public string Key { get; set; } = string.Empty;
        public long Size { get; set; }
    }

    public class EgressSendResult
    {
        public bool Success { get; set; }
        public string? Error { get; set; }

        public int? ExternalRequestId { get; set; }
        public int? ExternalRequestVersion { get; set; }
    }
}
