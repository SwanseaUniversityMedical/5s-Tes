using System.DirectoryServices.Protocols;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net;
using System.Linq.Expressions;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using Credentials.Camunda.Models;
using Serilog;


namespace Credentials.Camunda.Services
{
    public class LdapUserManagementService : ILdapUserManagementService
    {
        private readonly LdapSettings _config;
        private readonly ILogger<LdapUserManagementService> _logger;

        public LdapUserManagementService(IOptions<LdapSettings> config, ILogger<LdapUserManagementService> logger)
        {
            _config = config.Value;
            _logger = logger;
        }

        private LdapConnection CreateConnection()
        {
            LdapDirectoryIdentifier identifier = null;
            if (_config.Port == -1)
            {
                Log.Information("_config.Host > " + _config.Host + " _config.connectionless >  " + _config.connectionless);
                identifier = new LdapDirectoryIdentifier(_config.Host, fullyQualifiedDnsHostName : true, _config.connectionless);
            }
            else
            {
                Log.Information("_config.Host > " + _config.Host + " _config.Port >  " + _config.Port);
                identifier = new LdapDirectoryIdentifier(_config.Host, _config.Port);
            }

            // Always use the identifier which contains both host and port information
            var connection = new LdapConnection(identifier);

    
            connection.SessionOptions.ProtocolVersion = 3;
            Log.Information("_config.UseSSL > " + _config.UseSSL);
            if (_config.UseSSL)
            {
                connection.SessionOptions.SecureSocketLayer = true;
            }

            connection.AuthType = AuthType.Basic;
            Log.Information("admin DN > " + _config.AdminDn);

            connection.Credential = new NetworkCredential(_config.AdminDn, _config.AdminPassword);

            return connection;
        }
        
        public async Task<UserCreationResult> CreateUserAsync(CreateUserRequest request)
        {
            try
            {
                using var connection = CreateConnection();
                try
                {
                    connection.Bind();
                    _logger.LogInformation("LDAP bind successful.");
                } catch (System.DirectoryServices.Protocols.LdapException ex)
                {
                    Log.Error("LDAP connection failed: {Message} - ServerErrorMessage: {ServerError}", ex.Message, ex.ServerErrorMessage);
                    throw;
                }

                //Null check
                if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
                    return UserCreationResult.Error("Username and password are required");

                //Check if user already exists             
                if (await UserExistsAsync(request.Username))
                {
                    _logger.LogInformation("User {Username} already exists, skipping creation", request.Username);
                    return UserCreationResult.Ok(); 
                }

                var escapedCn = EscapeDnValue(request.Username);
                var userDn = $"cn={escapedCn},{_config.UserOu},{_config.BaseDn}";

                Log.Information("userDn >" + userDn);

                var addAttrs = new List<DirectoryAttribute>
                {
           
                    new DirectoryAttribute("objectClass", new[] { "top", "person", "organizationalPerson", "inetOrgPerson" }),
                    new DirectoryAttribute("cn", request.Username),
                    new DirectoryAttribute("sn", request.Username),
                    new DirectoryAttribute("uid", request.Username),
                    new DirectoryAttribute("userPassword", request.Password)
                };

                
                if (request.SchemaPermissions != null)
                {
                    var schemaPermissions = request.SchemaPermissions
                        .Where(p => !string.IsNullOrWhiteSpace(p.SchemaName))
                        .Select(p => $"{p.SchemaName}:{(int)p.Permissions}")
                        .ToArray();

                    if (schemaPermissions.Length > 0)
                    {
                        addAttrs.Add(new DirectoryAttribute("businessCategory", schemaPermissions));
                    }
                }

                var addRequest = new AddRequest(userDn, addAttrs.ToArray());
                var response = (AddResponse)connection.SendRequest(addRequest);

                if (response.ResultCode == ResultCode.Success)
                {
                    _logger.LogInformation($"user {request.Username} created successfully");
                    
                    // ─────────────────────────────
                    // ALEX CODE: add user to group
                    // ─────────────────────────────
                    var groupCn = $"{_config.GroupCn},{_config.BaseDn}";

                    Log.Information("groupCn >" + groupCn);
        
                    // TODO: add support for ldap servers that use 'memberUid' instead of 'member' attribute
                    // Will require querying the ldap server to see what it uses
                    var mod = new DirectoryAttributeModification
                    {
                        Name = "member",
                        Operation = DirectoryAttributeOperation.Add
                    };

                    mod.Add(userDn);

                    var modifyRequest = new ModifyRequest(groupCn, mod);
        
                    try
                    {
                        var modifyResponse = (ModifyResponse)connection.SendRequest(modifyRequest);
        
                        if (modifyResponse.ResultCode == ResultCode.Success)
                        {
                            _logger.LogInformation($"User {request.Username} added to group {groupCn} successfully");
                        }
                        else
                        {
                            _logger.LogWarning($"User {request.Username} created but failed to add to group {groupCn}: {modifyResponse.ErrorMessage}");
                        }
                    }
                    catch (DirectoryOperationException ex)
                    {
                        _logger.LogWarning($"User {request.Username} created but exception when adding to group {groupCn}: {ex.Message}");
                    }
                    // ─────────────────────────────
                    
                    return UserCreationResult.Ok();
                }
                else
                {
                    _logger.LogError($"Failed to create User {request.Username}:{response.ErrorMessage}");
                    return UserCreationResult.Error(response.ErrorMessage);
                }
            }           
            catch (Exception ex)
            {

                _logger.LogError(ex, $"Exception while creating user {request.Username}");
                return UserCreationResult.Error(ex.Message);
            }
        }

        public async Task<UserCreationResult> DeleteUserAsync(string username)
        {
            try
            {
                using var connection = CreateConnection();
                connection.Bind();

                //Check if user exists

                var userDn = $"cn={username}, {_config.UserOu}, {_config.BaseDn}";
                var deleteRequest = new DeleteRequest(userDn);

                var response = (DeleteResponse)connection.SendRequest(deleteRequest);

                if(response.ResultCode == ResultCode.Success)
                {
                    _logger.LogInformation($"User {username} deleted successfully");
                    return UserCreationResult.Ok();
                }
                else
                {
                    _logger.LogError($"Failed to delete user {username}: {response.ErrorMessage}");
                    return UserCreationResult.Error(response.ErrorMessage);
                }
            }
            catch(Exception ex) 
            {
                _logger.LogError(ex, $"Exception while deleting user {username}" );
                return UserCreationResult.Error(ex.Message );
            }
        }

        public async Task<bool> UserExistsAsync(string username)
        {
            using var connection = CreateConnection();
            connection.Bind();
            _logger.LogInformation("LDAP bind successful.");

            try
            {
                var searchRequest = new SearchRequest(
              $"{_config.UserOu},{_config.BaseDn}",
              $"(cn={username})",
              SearchScope.OneLevel,
              "cn");

                var response = (SearchResponse)connection.SendRequest(searchRequest);
                return response.Entries.Count > 0;
            }
            catch (System.DirectoryServices.Protocols.DirectoryOperationException ex) {
                return false;
            }
        }

        private static string EscapeDnValue(string value)
        {
            if (string.IsNullOrEmpty(value)) return value;

            var sb = new StringBuilder();
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                switch (c)
                {
                    case ',':
                    case '+':
                    case '"':
                    case '\\':
                    case '<':
                    case '>':
                    case ';':
                    case '=':
                        sb.Append('\\').Append(c);
                        break;
                    case '#':
                        if (i == 0) { sb.Append('\\').Append(c); } else { sb.Append(c); }
                        break;
                    case ' ':
                        if (i == 0 || i == value.Length - 1) { sb.Append('\\').Append(' '); } else { sb.Append(' '); }
                        break;
                    default:
                        sb.Append(c);
                        break;
                }
            }
            return sb.ToString();
        }        
    }
}
