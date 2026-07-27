using System.Text.Json;
using FiveSafesTes.Core.Models;
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
        Task DiscoverProjects();
    }

    public class UserService : IUserService
    {
        private readonly ILdapService _ldapService;
        private readonly ISubmissionClientHelper _clientHelper;
        private readonly JobSettings _jobSettings;

        private readonly List<string> _ouPath;

        public UserService(ILdapService ldapService, ISubmissionClientHelper clientHelper, JobSettings jobSettings, ActiveDirectorySettings adSettings) 
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
        /// Discovers which projects belong to this TRE and ensures each one has a recurring AD sync job registered.
        /// Safe to run repeatedly - SetupRecurringProjectCheck just refreshes the existing job registration.
        /// </summary>
        public async Task DiscoverProjects()
        {
            List<Project>? projects = await _clientHelper.CallAPIWithoutModel<List<Project>>("api/Project/GetAllProjectsForTre");
            if (projects == null) return;

            foreach (Project project in projects)
            {
                SetupRecurringProjectCheck(project.Name);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="projectName"></param>
        /// <returns></returns>
        public async Task UpdateGroupsForProject(string projectName)
        {
            List<ProjectUser> approvedUsers = await GetUsersForProject(projectName);
            if (approvedUsers == null) return;

            var approvedUsernames = approvedUsers.Select(u => u.Username).ToHashSet(StringComparer.OrdinalIgnoreCase);

            if (approvedUsers.Count > 0 && !_ldapService.CheckGroupExists(projectName))
            {
                ResultType groupCreationResult = CreateADGroup(projectName);
                if (groupCreationResult == ResultType.Failure) return;
            }

            // Add everyone currently approved by every TRE.
            foreach (ProjectUser user in approvedUsers)
            {
                if (!_ldapService.CheckUserExists(user.Username))
                {
                    ResultType userCreationResult = await AddUserToAD(user);
                    if (userCreationResult == ResultType.Failure) continue;
                }

                _ldapService.AddUserToGroup(user.Username, projectName);
            }

            // Remove anyone still in the group whose approval has since lapsed
            // (a TRE revoked/changed its decision, or they dropped off the project).
            if (!_ldapService.CheckGroupExists(projectName)) return;

            foreach (string existingMember in _ldapService.GetGroupMemberUsernames(projectName))
            {
                if (!approvedUsernames.Contains(existingMember))
                {
                    _ldapService.RemoveUserFromGroup(existingMember, projectName);
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
            List<string>? userJson = await _clientHelper.CallAPIWithoutModel<List<string>>($"api/GetApprovedUsersForProject/{Uri.EscapeDataString(projectName)}", httpMethod: HttpMethod.Get);

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
