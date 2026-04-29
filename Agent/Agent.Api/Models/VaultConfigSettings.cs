namespace Agent.Api.Models;

public class VaultConfigSettings
{
    public int TREId { get; set; }
    public string TREName { get; set; }
    public string SubmissionURL { get; set; }
    public string KeycloakRealmSettingURL { get; set; }
    public string JWT { get; set; }
    public bool IsConfigurationImported { get; set; }
}
