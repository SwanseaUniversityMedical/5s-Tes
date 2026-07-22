using System.Text.Json;
using FiveSafesTes.Core.Models.APISimpleTypeReturns;
using FiveSafesTes.Core.Services;
using Hangfire;
using TeleportUserManagement.Models;
using TeleportUserManagement.Models.Settings;
using TeleportUserManagement.Utilities;
using JobSettings = TeleportUserManagement.Models.Settings.JobSettings;

namespace TeleportUserManagement.Services
{
    public interface IUserService
    {
        void SetupRecurringProjectCheck(string projectName);
        Task UpdateGroupsForProject(string projectName);
    }

    public class UserService : IUserService
    {
        private readonly ILdapService _ldapService;
        private readonly IDareClientHelper _clientHelper;
        private readonly JobSettings _jobSettings;

        private readonly List<string> _ouPath;

        public UserService(ILdapService ldapService, IDareClientHelper clientHelper, JobSettings jobSettings, ActiveDirectorySettings adSettings) 
        {
            _ldapService = ldapService;
            _clientHelper = clientHelper;
            _jobSettings = jobSettings;

            _ouPath = adSettings.Connection.BaseOu.Split(',').ToList();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="projectName"></param>
        public void SetupRecurringProjectCheck(string projectName) 
        {
            string jobName = $"{_jobSettings.ProjectJobNamePrefix}_{projectName}";
            RecurringJob.AddOrUpdate<IUserService>(jobName, x => x.UpdateGroupsForProject(projectName), Cron.MinuteInterval(_jobSettings.ProjectCheckSchedule));
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="projectName"></param>
        /// <returns></returns>
        public async Task UpdateGroupsForProject(string projectName)
        {
            List<ProjectUser> users = await GetUsersForProject(projectName);
            if (users == null) return;

            if (await IsProjectApproved(projectName))
            {
                // The project has been unanimously approved, add the group to all of its corresponding users.
                if (!_ldapService.CheckGroupExists(projectName))
                {
                    ResultType groupCreationResult = CreateADGroup(projectName);
                    if (groupCreationResult == ResultType.Failure) return;
                }

                foreach (ProjectUser user in users)
                {
                    // Add users if they don't exist in AD already, then assign them the group.
                    if (!_ldapService.CheckUserExists(user.Username))
                    {
                        ResultType userCreationResult = await AddUserToAD(user);
                        if (userCreationResult == ResultType.Failure) continue;
                    }

                    // TODO check everyone has approved this user, then if not remove the group if they have it already
                    _ldapService.AddUserToGroup(user.Username, projectName);
                }
            }
            else
            {
                // One or more TRE has not approved this project, remove group from all users.
                if (!_ldapService.CheckGroupExists(projectName))
                {
                    // The group doesn't exist yet in AD.
                    return;
                }

                foreach (ProjectUser user in users)
                {
                    if (!_ldapService.CheckUserExists(user.Username)) continue;
                    _ldapService.RemoveUserFromGroup(user.Username, projectName);
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="projectName"></param>
        /// <returns></returns>
        private async Task<List<ProjectUser>> GetUsersForProject(string projectName)
        {
            List<string>? userJson = await _clientHelper.CallAPIWithoutModel<List<string>>($"api/GetUsersForApprovedProject/{Uri.EscapeDataString(projectName)}", httpMethod: HttpMethod.Get);

            if (userJson == null) return null;

            List<ProjectUser> users = [];

            foreach (string json in userJson) 
            {
                users.Add(JsonSerializer.Deserialize<ProjectUser>(json));
            }

            return users;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="projectName"></param>
        /// <returns></returns>
        private async Task<bool> IsProjectApproved(string projectName)
        {
            BoolReturn? result = await _clientHelper.CallAPIWithoutModel<BoolReturn>($"api/IsProjectApproved/{Uri.EscapeDataString(projectName)}", httpMethod: HttpMethod.Get);
            return result?.Result ?? false;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="user"></param>
        private async Task<ResultType> AddUserToAD(ProjectUser user)
        {
            return await _ldapService.CreateUserAccount(user.Username, user.FullName, "", user.Email, "", true, false, true, _ouPath);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="groupName"></param>
        private ResultType CreateADGroup(string groupName)
        {
            return _ldapService.CreateGroup(groupName, "", _ouPath);
        }
    }
}
