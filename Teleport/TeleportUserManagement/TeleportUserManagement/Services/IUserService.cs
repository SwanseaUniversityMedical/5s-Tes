using System.Text.Json;
using FiveSafesTes.Core.Models;
using FiveSafesTes.Core.Services;
using Hangfire;
using Hangfire.Storage;
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
            List<Project.ProjectSummary>? projects = await _clientHelper.CallAPIWithoutModel<List<Project.ProjectSummary>>("/api/Project/GetAllProjects?responseType=summary");
            if (projects == null) return;

            List<Project.ProjectSummary> teleportProjects = projects.Where(x => x.ProjectType == ProjectType.Teleport).ToList();

            foreach (Project.ProjectSummary project in teleportProjects)
            {
                SetupRecurringProjectCheck(project.Name);
            }

            PruneNonTeleportProjects(teleportProjects);
        }

        /// <summary>
        /// Checks that all of our active hangfire jobs are still for Teleport projects and removes any that have since changed their type.
        /// </summary>
        /// <param name="teleportProjects">The list of approved projects that are of ProjectType Teleport.</param>
        private void PruneNonTeleportProjects(List<Project.ProjectSummary> teleportProjects) 
        {
            HashSet<string> expectedJobIds = teleportProjects.Select(x => $"{_jobSettings.ProjectJobNamePrefix}_{x.Name}").ToHashSet();

            IEnumerable<string> registeredJobIds = JobStorage.Current.GetConnection().GetRecurringJobs().Select(x => x.Id)
                .Where(id => id.StartsWith($"{_jobSettings.ProjectJobNamePrefix}_"));

            foreach (string jobId in registeredJobIds)
            {
                if (!expectedJobIds.Contains(jobId))
                {
                    RecurringJob.RemoveIfExists(jobId);

                    // Remove all users from this group as the project is no longer of type Teleport
                    string projectName = jobId.Substring($"{_jobSettings.ProjectJobNamePrefix}_".Length);
                    foreach (string username in _ldapService.GetGroupMemberUsernames(projectName))
                    {
                        _ldapService.RemoveUserFromGroup(username, projectName);
                    }
                }
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

            // Users must already exist in Active Directory - skip those who do not.
            foreach (ProjectUser user in approvedUsers)
            {
                if (!_ldapService.CheckUserExists(user.Username))
                {
                    continue;
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
        /// Creates a new group in Active Directory.
        /// </summary>
        /// <param name="groupName">The name of our new AD group.</param>
        private ResultType CreateADGroup(string groupName)
        {
            return _ldapService.CreateGroup(groupName, "", _ouPath);
        }
    }
}
