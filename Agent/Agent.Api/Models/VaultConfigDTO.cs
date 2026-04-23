using FiveSafesTes.Core.Models.Settings;

namespace Agent.Api.Models;

public class VaultConfigDTO
{
    public VaultConfigSettings VaultConfigSettings { get; set; }
    public SubmissionKeyCloakSettings SubmissionKeyCloakSettings { get; set; }
}
