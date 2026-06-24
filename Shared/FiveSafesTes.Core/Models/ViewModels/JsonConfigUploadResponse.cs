namespace FiveSafesTes.Core.Models.ViewModels;

public class JsonConfigUploadResponse
{
    public bool Success { get; set; }
    public string Message { get; set; }
    public List<HealthCheckStatus> HealthCheckStatus { get; set; }
    public bool IsSynced { get; set; }
    public bool IsUploaded { get; set; }
    public bool SyncJobExists { get; set; }
}
