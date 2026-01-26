
namespace FiveSafesTes.Core.Models.ViewModels
{
    public class OPASettings
    {
        public string? OPAUrl { get; set; }
        public double ExpiryDelayMinutes { get; set; }
        public bool UseRealExpiry { get; set; }

        public string OPAPolicyUploadURL { get; set; }
    }
}

