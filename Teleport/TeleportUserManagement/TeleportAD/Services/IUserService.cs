using TeleportAD.Models;
using TeleportAD.Models.Settings;
using TeleportAD.Utilities;

namespace TeleportAD.Services
{
    public interface IUserService
    {
        Task UpdateGroupsForProject(string projectName);
        Task<ProjectApprovalStatus> GetProjectApprovalStatus(string projectName);
        Task<List<ProjectUser>> GetUsersForProject(string projectName);
    }

    public class UserService : IUserService
    {
        private readonly ILdapService _ldapService;
        private readonly DareControlSettings _dareSettings;

        public UserService(ILdapService ldapService, DareControlSettings dareSettings) 
        {
            _ldapService = ldapService;
            _dareSettings = dareSettings;
        }


        public async Task UpdateGroupsForProject(string projectName)
        {
            List<ProjectUser> users = await GetUsersForProject(projectName);

            if (await GetProjectApprovalStatus(projectName) == ProjectApprovalStatus.Approved)
            {
                if (!_ldapService.CheckGroupExists(projectName)) CreateADGroup(projectName);

                foreach (ProjectUser user in users)
                {
                    if (!_ldapService.CheckUserExists(user.Username)) AddUserToAD(user);
                    _ldapService.AddUserToGroup(user.Username, projectName);
                }
            }
            else
            {
                foreach (ProjectUser user in users)
                {
                    _ldapService.RemoveUserFromGroup(user.Username, projectName);
                }
            }
        }

        public async Task<ProjectApprovalStatus> GetProjectApprovalStatus(string projectName)
        {
            throw new NotImplementedException();
        }


        public async Task<List<ProjectUser>> GetUsersForProject(string projectName)
        {
            throw new NotImplementedException();
        }

        private void AddUserToAD(ProjectUser user)
        {
            List<string> ouPath = new();
            _ldapService.CreateUserAccount(user.Username, user.GivenName, user.Surname, user.Email, "", true, false, true, ouPath);
        }

        private void CreateADGroup(string groupName)
        {
            List<string> ouPath = new();
            _ldapService.CreateGroup(groupName, "", ouPath);
        }
    }
}
