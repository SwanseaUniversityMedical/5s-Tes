namespace FiveSafesTes.Core.Models.ViewModels
{
    /// <summary>
    /// Onboarding configuration file produced for a TRE by the Submission platform.
    /// The TRE admin downloads this and provides it to the Agent on first startup.
    /// The Agent uses the JWT to call the Onboarding API and retrieve its long-term
    /// service account credentials from Vault.
    /// </summary>
    public class TreOnboardingConfig
    {
        public int TREId { get; set; }
        public string TREName { get; set; } = string.Empty;
        public string SubmissionURL { get; set; } = string.Empty;
        public string KeycloakRealmSettingURL { get; set; } = string.Empty;
        public string JWT { get; set; } = string.Empty;
        public bool IsConfigurationImported { get; set; }
    }
}
