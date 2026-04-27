using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using FiveSafesTes.Core.Models;
using FiveSafesTes.Core.Models.Settings;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using Submission.Api.Services.Contract;

namespace Submission.Api.Services
{
    public class OnboardingJwtService : IOnboardingJwtService
    {
        private readonly OnboardingJwtSettings _settings;

        public OnboardingJwtService(OnboardingJwtSettings settings)
        {
            _settings = settings;
        }

        public string GenerateOnboardingJwt(Tre tre)
        {
            if (string.IsNullOrWhiteSpace(_settings.Secret))
            {
                throw new InvalidOperationException("OnboardingJwt:Secret is not configured");
            }

            var now = DateTime.UtcNow;
            var expires = now.AddHours(_settings.LifetimeHours);

            var claims = new List<Claim>
            {
                new(JwtRegisteredClaimNames.Sub, tre.AdminUsername ?? string.Empty),
                new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new(JwtRegisteredClaimNames.Iat,
                    new DateTimeOffset(now).ToUnixTimeSeconds().ToString(),
                    ClaimValueTypes.Integer64),
                new("tre_id", tre.Id.ToString()),
                new("tre_name", tre.Name ?? string.Empty)
            };

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.Secret));
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _settings.Issuer,
                audience: _settings.Audience,
                claims: claims,
                notBefore: now,
                expires: expires,
                signingCredentials: credentials);

            var jwt = new JwtSecurityTokenHandler().WriteToken(token);

            Log.Information("{Function} Generated onboarding JWT for TRE {TreName} (id={TreId}), expires {Expires}",
                "GenerateOnboardingJwt", tre.Name, tre.Id, expires);

            return jwt;
        }
    }
}
