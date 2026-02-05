using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Credentials.Camunda.Services
{
    public interface IVaultCredentialsService
    {
        Task<bool> AddCredentialAsync(string path, Dictionary<string, object> credential);
        Task<bool> RemoveCredentialAsync(string path);
        Task<Dictionary<string, object>> GetCredentialAsync(string path);
        Task<bool> UpdateCredentialAsync(string path, Dictionary<string, object> credential);
        Task<string> GetConnectionStringAsync(string databaseName);
        Task<bool> StoreConnectionStringAsync(string databaseName, string server, string database, string username, string password, int port = 5432);
    }

}


