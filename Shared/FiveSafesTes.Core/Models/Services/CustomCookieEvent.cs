using IdentityModel.Client;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using FiveSafesTes.Core.Models.Settings;
using System.IdentityModel.Tokens.Jwt;


namespace FiveSafesTes.Core.Models.Services
{
    public class CustomCookieEvent : CookieAuthenticationEvents
    {
        private readonly BaseKeyCloakSettings _keyCloakSettings;

        public CustomCookieEvent(BaseKeyCloakSettings keyCloakSettings)
        {
            _keyCloakSettings = keyCloakSettings;
        }

        public override async Task ValidatePrincipal(CookieValidatePrincipalContext context)
        {
            if (context != null)
            {
                var accessToken = context.Properties.GetTokenValue("access_token");
                if (!string.IsNullOrEmpty(accessToken))
                {
                    var handler = new JwtSecurityTokenHandler();
                    var tokenS = handler.ReadToken(accessToken) as JwtSecurityToken;
                    var tokenExpiryDate = tokenS.ValidTo;
                    //// If there is no valid `exp` claim then `ValidTo` returns DateTime.MinValue
                    //if (tokenExpiryDate == DateTime.MinValue) throw new Exception("Could not get exp claim from token");
                    if (tokenExpiryDate < DateTime.UtcNow)
                    {
       
                        var refreshToken = context.Properties.GetTokenValue("refresh_token");

                        //check if users refresh token is still valid?
                        var tokenRefresh = handler.ReadToken(refreshToken) as JwtSecurityToken;
                        var refreshTokenExpiryDate = tokenRefresh.ValidTo;
                        if (refreshTokenExpiryDate < DateTime.UtcNow)
                        {
                         
                            //probably need to log user out
                            await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                        }
                        else
                        {
                            try
                            {
                                var tokenResponse = await new HttpClient().RequestRefreshTokenAsync(new RefreshTokenRequest
                                {
                                    Address = _keyCloakSettings.Authority + "/protocol/openid-connect/token",
                                    ClientId = _keyCloakSettings.ClientId,
                                    ClientSecret = _keyCloakSettings.ClientSecret,
                                    RefreshToken = refreshToken
                                });
                                if (tokenResponse.HttpStatusCode == System.Net.HttpStatusCode.OK)
                                {
                                    context.Properties.UpdateTokenValue("access_token", tokenResponse.AccessToken);
                                    context.Properties.UpdateTokenValue("refresh_token", tokenResponse.RefreshToken);
                                    await context.HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, context.Principal, context.Properties);
                                }
                                else
                                {
                                 
                                    await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme); 
                                }
                            }
                            catch (Exception ex)
                            {
                                await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                            }
                        }
                    }
                }
            }
        }
    }
    
}
