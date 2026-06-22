using System;
using System.Collections.Generic;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Amazon.Runtime.Internal.Transform;
using Credentials.Camunda.Models;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Serilog;
using Zeebe.Client.Accelerator.Abstractions;
using Zeebe.Client.Accelerator.Attributes;
using static System.Net.WebRequestMethods;


namespace AirlockFileReview.Camunda.BPMNHandlers
{
    [JobType("ValidateJWT")]
    [MaxJobsActive(1)]
    public class ValidateJWT : IAsyncZeebeWorkerWithResult<Dictionary<string, object>>
    {
        private readonly FlowSettings _FlowSettings;

        public ValidateJWT(FlowSettings _FlowSettings)
        {
            this._FlowSettings = _FlowSettings;
        }

        public async Task<Dictionary<string, object>> HandleJob(ZeebeJob job, CancellationToken cancellationToken)
        {
            var variables = JObject.Parse(job.Variables);

            var JWTToken = variables["JWTToken"].ToString();

            var _http = new HttpClient();

            var jwksJson = await _http.GetStringAsync(_FlowSettings.JWT);
            var keys = JObject.Parse(jwksJson)["keys"] as JArray;

            var tokenHandler = new JwtSecurityTokenHandler();
            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuer = false,
                ValidIssuer = null,
                ValidateAudience = false,
                ValidateLifetime = true,
                RequireSignedTokens = true,
                ValidateIssuerSigningKey = true,
                ClockSkew = TimeSpan.FromMinutes(2)
            };

            var signingKeys = new List<SecurityKey>();
            foreach (var key in keys)
            {
                if (key.Value<string>("kty") != "RSA") continue;
                var e = Base64UrlEncoder.DecodeBytes(key.Value<string>("e"));
                var n = Base64UrlEncoder.DecodeBytes(key.Value<string>("n"));
                var rsaParams = new System.Security.Cryptography.RSAParameters { Exponent = e, Modulus = n };
                signingKeys.Add(new RsaSecurityKey(rsaParams) { KeyId = key.Value<string>("kid") });
            }
            validationParameters.IssuerSigningKeys = signingKeys;
            bool JWTauthenticated = false;
            try
            {
                var principal = tokenHandler.ValidateToken(JWTToken, validationParameters, out var validatedToken);

                var realmAccess = principal.FindFirst("realm_access")?.Value;
                if (realmAccess != null)
                {
                    // realm_access is a JSON object string; parse it to inspect roles
                    var j = JObject.Parse(realmAccess);
                    var roles = j["roles"] as JArray;
                    if (roles != null && roles.Any(r => r.Value<string>() == _FlowSettings.RequiredRole))
                        JWTauthenticated = true;
                    else
                    {
                        JWTauthenticated = false;
                    }
                }
                else
                {
                    JWTauthenticated = false;
                }

             
            }
            catch (SecurityTokenException ex)
            {
                JWTauthenticated = false;
            }

            return new Dictionary<string, object>() { { "JWTauthenticated" , JWTauthenticated } };
        }
    }
}
