using Credentials.Camunda.Models;

namespace Credentials.Camunda.Services
{
    public interface ILdapUserManagementService
    {
        Task<UserCreationResult> CreateUserAsync(CreateUserRequest request);

        Task<UserCreationResult> DeleteUserAsync(string username);  
        
        Task<bool> UserExistsAsync(string username);
    }
}
