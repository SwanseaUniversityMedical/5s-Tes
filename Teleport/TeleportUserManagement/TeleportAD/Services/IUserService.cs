using System.Text.Json;
using FiveSafesTes.Core.Models.APISimpleTypeReturns;
using FiveSafesTes.Core.Services;
using Hangfire;
using TeleportUserManagement.Models;
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

        public UserService(ILdapService ldapService, IDareClientHelper clientHelper, JobSettings jobSettings) 
        {
            _ldapService = ldapService;
            _clientHelper = clientHelper;
            _jobSettings = jobSettings;
        }

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
                if (!_ldapService.CheckGroupExists(projectName)) CreateADGroup(projectName);

                foreach (ProjectUser user in users)
                {
                    // Add users if they don't exist in AD already, then assign them the group.
                    if (!_ldapService.CheckUserExists(user.Username)) AddUserToAD(user);
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
            List<string>? userJson = await _clientHelper.CallAPIWithoutModel<List<string>>($"api/GetUsersForApprovedProject/{projectName}", httpMethod: HttpMethod.Get);

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
            BoolReturn result = await _clientHelper.CallAPIWithoutModel<BoolReturn>($"api/IsProjectApproved/{projectName}", httpMethod: HttpMethod.Get);
            return result.Result;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="user"></param>
        private void AddUserToAD(ProjectUser user)
        {
            List<string> ouPath = new();
            _ldapService.CreateUserAccount(user.Username, user.FullName, "", user.Email, "", true, false, true, ouPath);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="groupName"></param>
        private void CreateADGroup(string groupName)
        {
            List<string> ouPath = new();
            _ldapService.CreateGroup(groupName, "", ouPath);
        }
    }
}
