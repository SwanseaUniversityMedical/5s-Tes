namespace FiveSafesTes.Core.Models
{
    /// <summary>
    /// Tracks onboarding JWT identifiers (jti claim) that have already been
    /// redeemed via the Onboarding API to enforce one-time use.
    /// </summary>
    public class UsedOnboardingJti
    {
        public int Id { get; set; }
        public string Jti { get; set; } = string.Empty;
        public int TreId { get; set; }
        public DateTime UsedAt { get; set; } = DateTime.UtcNow;
    }
}
