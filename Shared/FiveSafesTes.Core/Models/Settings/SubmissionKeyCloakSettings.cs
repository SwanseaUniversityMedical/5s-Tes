using FiveSafesTes.Core.Models.Enums;

namespace FiveSafesTes.Core.Models.Settings
{
    public class SubmissionKeyCloakSettings: BaseKeyCloakSettings
    {
        public string? Username { get; set; }
        public string? PasswordEnc { get; set; }
        public ConfigInputMethod ConfigInputMethod { get; set; } = ConfigInputMethod.Manual;
    }
}
