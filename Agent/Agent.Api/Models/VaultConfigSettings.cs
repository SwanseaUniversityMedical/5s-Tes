namespace Agent.Api.Models;

public class VaultConfigSettings
{
    public string TREName { get; set; }
    public string SubmissionURL { get; set; }
    public string KeycloakRealmSettingURL { get; set; }
    public string Username { get; set; }
    public string JWT { get; set; }
    public string Password { get; set; }
    public string VaultConfigPath { get; set; }
}
