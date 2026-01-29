using System.Security.Claims;
using Agent.Api.Repositories.DbContexts;
using FiveSafesTes.Core.Models;
using FiveSafesTes.Core.Models.APISimpleTypeReturns;
using FiveSafesTes.Core.Services;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace Agent.Api.Services
{
    public class ControllerHelpers
    {
        public static async Task AddTreAuditLog(TreProject? project, TreMembershipDecision? membershipDecision, bool approved, ApplicationDbContext dbContext, IHttpContextAccessor httpContextAccessor, ClaimsPrincipal loggedInUser)
        {
            var audit = new TreAuditLog()
            {
                Approved = approved,
                IPaddress = httpContextAccessor.HttpContext.Connection.RemoteIpAddress.ToString(),
                ApprovedBy = (from x in loggedInUser.Claims where x.Type == "preferred_username" select x.Value).First(),
                Date = DateTime.Now.ToUniversalTime(),
                Project = project,
                MembershipDecision = membershipDecision,
            };
            dbContext.TreAuditLogs.Add(audit);
            await dbContext.SaveChangesAsync();
            Log.Information("{Function}: AuditLogs: ProjectID: {ProjectID}, Membership {Membership}, Approved: {Approved}, ApprovedBy {ApprovedBy}", "AddTreAuditLog", project == null ? "[null]": project.Id, membershipDecision == null ? "[null]" : membershipDecision.Id, approved, (from x in loggedInUser.Claims where x.Type == "preferred_username" select x.Value).First());
        }

        public static async Task<BoolReturn> CheckCredentialsAreValid(KeycloakTokenHelper keycloakTokenHelper, IEncDecHelper encDecHelper, ApplicationDbContext dbContext, CredentialType type)
        {
            try
            {
                var result = new BoolReturn() { Result = false };
                var creds = dbContext.KeycloakCredentials.FirstOrDefault(x =>
                    x.CredentialType == type);
                if (creds != null)
                {
                    if (string.IsNullOrWhiteSpace(creds.UserName))
                    {
                        var sdfsdf = 1;
                    }
                    var token = await keycloakTokenHelper.GetTokenForUser(creds.UserName,
                        encDecHelper.Decrypt(creds.PasswordEnc), "dare-tre-admin");
                    result.Result = !string.IsNullOrWhiteSpace(token.token);
                }

                return result;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "{Function} Crash", "CheckCredentialsAreValid");
                throw;
            }
        }

        public static async Task<KeycloakCredentials> UpdateCredentials(KeycloakCredentials creds, KeycloakTokenHelper keycloakTokenHelper,
            ApplicationDbContext DbContext, IEncDecHelper encDecHelper, CredentialType type, string requiredrole)
        {
            try
            {
                creds.Valid = true;
                var token = await keycloakTokenHelper.GetTokenForUser(creds.UserName,
                    creds.PasswordEnc, requiredrole);
                if (string.IsNullOrWhiteSpace(token.token))
                {
                    Log.Information($"UpdateCredentials creds.Valid = false  for {creds.UserName}");
                    creds.ErrorMessage = token.Errorstring;
                    creds.Valid = false;
                    return creds;
                }

                var add = true;
                var dbcred = DbContext.KeycloakCredentials.FirstOrDefault(x => x.CredentialType == type);
                if (dbcred != null)
                {
                    creds.Id = dbcred.Id;
                    
                    add = false;
                    DbContext.Entry(dbcred).State = EntityState.Detached;
                }

               

                creds.CredentialType = type;
                creds.PasswordEnc = encDecHelper.Encrypt(creds.PasswordEnc);
                if (add)
                {
                    DbContext.KeycloakCredentials.Add(creds);

                }
                else
                {
                    DbContext.KeycloakCredentials.Update(creds);
                }

                await DbContext.SaveChangesAsync();

                Log.Information("{Function} Credentials Successfully update", "UpdateCredentials");
                return creds;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "{Function} Crash", "UpdateCredentials");
                throw;
            }
        }
    }
}
