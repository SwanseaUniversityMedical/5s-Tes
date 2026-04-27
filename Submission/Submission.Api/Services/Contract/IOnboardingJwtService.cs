using FiveSafesTes.Core.Models;

namespace Submission.Api.Services.Contract
{
    public interface IOnboardingJwtService
    {
        /// <summary>
        /// Generates a short-lived, one-time-use JWT for a TRE onboarding flow.
        /// The JWT is embedded in the downloadable config file and later presented
        /// by the Agent to the Onboarding API to retrieve its long-term credentials.
        /// </summary>
        string GenerateOnboardingJwt(Tre tre);
    }
}
