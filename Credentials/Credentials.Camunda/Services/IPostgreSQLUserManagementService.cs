using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Credentials.Camunda.Models;

namespace Credentials.Camunda.Services
{
    public interface IPostgreSQLUserManagementService
    {
        Task<UserCreationResult> CreateUserAsync(CreateUserRequest request);
        Task<bool> UserExistsAsync(string username);
        Task<bool> DropUserAsync(string username);
        Task<List<string>> GetUserSchemasAsync(string username);
    }
}
