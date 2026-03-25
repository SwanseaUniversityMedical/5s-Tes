using System.Security.Claims;
using FiveSafesTes.Core.Models;
using Microsoft.AspNetCore.Authentication;
using Serilog;
using Submission.Api.Repositories.DbContexts;
using Submission.Api.Services.Contract;

namespace Submission.Api.Services
{
    public class ControllerHelpers
    {

        public static async Task AddUserToMinioBucket(User user, Project project,
            IHttpContextAccessor httpContextAccessor, string attributeName,
            IKeycloakMinioUserService keycloakMinioUserService, ClaimsPrincipal loggedInUser,
            ApplicationDbContext dbContext)
        {
            var accessToken = await httpContextAccessor.HttpContext.GetTokenAsync("access_token");


            await keycloakMinioUserService.SetMinioUserAttribute(accessToken, user.Name.ToString(), attributeName,
                project.SubmissionBucket.ToLower() + "_policy");

            await keycloakMinioUserService.SetMinioUserAttribute(accessToken, user.Name.ToString(), attributeName,
                project.OutputBucket.ToLower() + "_policy");
            



        }


        public static Tre? GetUserTre(ClaimsPrincipal loggedInUser, ApplicationDbContext dbContext)
        {
            var usersName = (from x in loggedInUser.Claims where x.Type == "preferred_username" select x.Value).First();
            var tre = dbContext.Tres.FirstOrDefault(x => x.AdminUsername.ToLower() == usersName.ToLower());
            if (tre == null)
            {
                throw new Exception("User " + usersName + " doesn't have a tre");
            }

            return tre;
        }


        public static async Task RemoveUserFromMinioBucket(User user, Project project,
            IHttpContextAccessor httpContextAccessor, string attributeName,
            IKeycloakMinioUserService keycloakMinioUserService, ClaimsPrincipal loggedInUser,
            ApplicationDbContext dbContext)
        {
            var accessToken = await httpContextAccessor.HttpContext.GetTokenAsync("access_token");



            await keycloakMinioUserService.RemoveMinioUserAttribute(accessToken, user.Name.ToString(), attributeName,
                project.SubmissionBucket.ToLower() + "_policy");
            await keycloakMinioUserService.RemoveMinioUserAttribute(accessToken, user.Name.ToString(), attributeName,
                project.OutputBucket.ToLower() + "_policy");

            


        }

        public static async Task AddAuditLog(LogType logType, User? user, Project? project, Tre? tre, FiveSafesTes.Core.Models.Submission? submission, string? formData,
            IHttpContextAccessor httpContextAccessor,
            ClaimsPrincipal loggedInUser, ApplicationDbContext dbContext)
        {
            var audit = new AuditLog()
            {
                HistoricFormData = formData,
                Submission = submission,
                IPaddress = httpContextAccessor.HttpContext.Connection.RemoteIpAddress.ToString(),
                LoggedInUserName = (from x in loggedInUser.Claims where x.Type == "preferred_username" select x.Value).First(),
                Project = project,
                User = user,
                Tre = tre,
                LogType = logType,
                Date = DateTime.Now.ToUniversalTime()
            };
            dbContext.AuditLogs.Add(audit);
            await dbContext.SaveChangesAsync();
            Log.Information(
                "{Function}: AuditLogs: LogType: {LogType,} UserId: {UserId}, ProjectId: {ProjectId}, TreId: {TreId}, SubmissionId: {SubmissionId}, FormData {FormData}, LoggedInUser: {LoggedInUser}",
                "AddAuditLog", logType.ToString(), user == null ? "[null]" : user.Id, project == null ? "[null]" : project.Id,
                tre == null ? "[null]" : tre.Id, submission == null ? "[null]" : submission.Id, formData == null ? "[null]" : formData,
                (from x in loggedInUser.Claims where x.Type == "preferred_username" select x.Value).First());
        }
    }
}
