namespace FiveSafesTes.Core.Models.ViewModels
{
    /// <summary>
    /// Response returned by the Submission Onboarding API when an Agent successfully
    /// exchanges its temporary onboarding JWT for its long-term Keycloak service account
    /// credentials retrieved from Vault.
    /// </summary>
    public class OnboardingCredentialsResponse
    {
        public int TreId { get; set; }
        public string TreName { get; set; } = string.Empty;
        public string ClientId { get; set; } = string.Empty;
        public string ClientSecret { get; set; } = string.Empty;
    }
}
