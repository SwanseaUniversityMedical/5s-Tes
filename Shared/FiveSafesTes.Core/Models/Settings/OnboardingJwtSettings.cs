namespace FiveSafesTes.Core.Models.Settings
{
    /// <summary>
    /// Settings for the temporary onboarding JWT that is embedded in the TRE config
    /// file. Signed with a symmetric HMAC secret (HS256) by the Submission platform,
    /// then later verified when the Agent calls the Onboarding API to exchange it
    /// for its long-term service account credentials.
    /// </summary>
    public class OnboardingJwtSettings
    {
        public string Secret { get; set; } = string.Empty;
        public string Issuer { get; set; } = "Submission.Api";
        public string Audience { get; set; } = "Agent.Onboarding";
        public int LifetimeHours { get; set; } = 24;
    }
}
