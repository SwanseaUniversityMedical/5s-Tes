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
        /// Sets up a recurring job to ensure that all users in the active directory group still belong there.
        /// </summary>
        /// <param name="projectName">The name of the project we are checking.</param>
        public void SetupRecurringProjectCheck(string projectName)
        {
            string jobName = $"{_jobSettings.ProjectJobNamePrefix}_{projectName}";
            RecurringJob.AddOrUpdate<IUserService>(jobName, x => x.UpdateGroupsForProject(projectName), Cron.MinuteInterval(_jobSettings.ProjectCheckSchedule));
        }

        /// <summary>
        /// Discovers all existing projects and ensures each one has a recurring AD sync job registered.
        /// </summary>
        public async Task DiscoverProjects()
        {
            List<Project>? projects = await _clientHelper.CallAPIWithoutModel<List<Project>>("/api/Project/GetAllProjects");
            if (projects == null) return;

            foreach (Project project in projects)
            {
                SetupRecurringProjectCheck(project.Name);
            }
        }

        /// <summary>
        /// Add or remove an Active Directory group from project users based on their approval status.
        /// </summary>
        /// <param name="projectName">The name of the project we are managing the users from.</param>
        public async Task UpdateGroupsForProject(string projectName)
        {
            List<ProjectUser> approvedUsers = await GetUsersForProject(projectName);
            if (approvedUsers == null) return;

            // Ensure each user name only appears in the list once
            var approvedUsernames = approvedUsers.Select(u => u.Username).ToHashSet(StringComparer.OrdinalIgnoreCase);

            if (approvedUsers.Count > 0 && !_ldapService.CheckGroupExists(projectName))
            {
                // No AD group exists for this project, create it.
                ResultType groupCreationResult = CreateADGroup(projectName);
                if (groupCreationResult == ResultType.Failure) return;
            }

            // Add every user currently approved by every TRE to active directory
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
        /// Returns a list of project users who have been unanimously approved for a given project.
        /// </summary>
        /// <param name="projectName">The name of the project we are retrieving our approved users from.</param>
        private async Task<List<ProjectUser>> GetUsersForProject(string projectName)
        {
            List<string>? userJson = await _clientHelper.CallAPIWithoutModel<List<string>>($"/api/Project/GetApprovedUsersForProject/{Uri.EscapeDataString(projectName)}", httpMethod: HttpMethod.Get);

            if (userJson == null) return null;

            List<ProjectUser> users = [];

            foreach (string json in userJson) 
            {
                users.Add(JsonSerializer.Deserialize<ProjectUser>(json));
            }

            return users;
        }

        /// <summary>
        /// Adds a new user to Active Directory.
        /// </summary>
        /// <param name="user">The details of the user we wish to add.</param>
        private async Task<ResultType> AddUserToAD(ProjectUser user)
        {
            return await _ldapService.CreateUserAccount(user.Username, user.FullName, "", user.Email, "", true, false, true, _ouPath);
        }

        /// <summary>
        /// Creates a new group in Active Directory.
        /// </summary>
        /// <param name="groupName">The name of our new AD group.</param>
        private ResultType CreateADGroup(string groupName)
        {
            return _ldapService.CreateGroup(groupName, "", _ouPath);
        }
    }
}
