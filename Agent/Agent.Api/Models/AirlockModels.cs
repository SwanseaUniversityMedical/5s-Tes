namespace Agent.Api.Models
{
    /// <summary>
    /// Mirrors Director-Airlock's FileStatus. Used for both a request and each of its files.
    /// For a file: Completed = approved, Rejected = rejected, PartiallyApproved = reviewers disagreed.
    /// </summary>
    public enum AirlockFileStatus
    {
        Draft = 0,
        PendingReview = 1,
        Removed = 2,
        Pending = 3,
        InReview = 4,
        Completed = 5,
        Rejected = 6,
        Revoked = 7,
        Withdrawn = 8,
        PartiallyApproved = 9,
        OutputReady = 10,
    }

    /// <summary>
    /// The JSON "request" part of Airlock's SubmitExternalRequest call.
    /// </summary>
    public class AirlockExternalRequest
    {
        // The 5S-TES submission id. Airlock returns the existing request for a repeat call.
        public string ExternalReference { get; set; } = string.Empty;

        // Checked by Airlock against the token's client and audience.
        public string ClientType { get; set; } = "FiveSafesTes";
        public string ProjectId { get; set; } = string.Empty;
        public string RequestName { get; set; } = string.Empty;
        public string? Plane { get; set; }
        public string? Product { get; set; }
        public Dictionary<string, string> Metadata { get; set; } = new();
        public List<AirlockExternalRequestFile> Files { get; set; } = new();
    }

    public class AirlockExternalRequestFile
    {
        public string Filename { get; set; } = string.Empty;
        public string? Comment { get; set; }
        public string? Classification { get; set; }
    }

    /// <summary>
    /// One file to send to Airlock. The stream is opened only when the request body is written,
    /// so files are streamed from MinIO one at a time instead of being held in memory.
    /// </summary>
    public class AirlockUploadFile
    {
        public string Filename { get; set; } = string.Empty;
        public Func<Task<Stream>> OpenStream { get; set; } = null!;
    }

    /// <summary>
    /// Airlock's FileRequestDTO, returned by SubmitExternalRequest and GetRequest.
    /// </summary>
    public class AirlockFileRequest
    {
        public int RequestId { get; set; }
        public string? RequestName { get; set; }
        public int Version { get; set; }
        public AirlockFileStatus Status { get; set; }
        public string? ProjectId { get; set; }
        public AirlockRequestObject? RequestObject { get; set; }
    }

    public class AirlockRequestObject
    {
        public int RequestObjectId { get; set; }
        public string? BaseLocation { get; set; }
        public List<AirlockFile> Files { get; set; } = new();
    }

    public class AirlockFile
    {
        public int FileId { get; set; }
        public string? Filename { get; set; }
        public string? Size { get; set; }
        public string? Comment { get; set; }
        public AirlockFileStatus Status { get; set; }
    }

    /// <summary>
    /// What Airlock POSTs to the review DMN's APIsCallOnCompletion URLs once a review finishes
    /// (approved, partially approved or rejected): its file-review FileRequest, serialised by the
    /// Camunda worker's JsonOutputToAPIs step. Only the fields 5S-TES needs are mapped.
    /// </summary>
    public class AirlockReviewOutcome
    {
        public int RequestId { get; set; }
        public int Version { get; set; }
        public AirlockFileStatus Status { get; set; }

        // "Fully approved", "Partially approved" or "Rejected".
        public string? OutcomeStatus { get; set; }

        // JSON array of { FileID, FileName } for the files every reviewer approved.
        public string? ApprovedFiles { get; set; }

        // JSON object sent by 5S-TES with the request, e.g. {"subId":"16",...}.
        public string? Metadata { get; set; }

        public AirlockRequestObject? RequestObject { get; set; }
    }
}
