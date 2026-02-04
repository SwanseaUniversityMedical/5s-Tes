using Credentials.Camunda.Services;
using Zeebe.Client.Accelerator.Attributes;

namespace Credentials.Camunda.ProcessHandlers
{
    [JobType("delete-trino-user")]
    public class DeleteTrinoUserHandler : DeleteCredentialHandlerBase
    {
        private readonly ILdapUserManagementService _ldapUserManagementService;
        protected override string CredentialType => "trino";

        public DeleteTrinoUserHandler(
            ILogger<DeleteTrinoUserHandler> logger,
            ILdapUserManagementService ldapUserManagementService,
            IVaultCredentialsService vaultCredentialsService,
            IEphemeralCredentialsService ephemeralCredentialsService)
            : base(logger, vaultCredentialsService, ephemeralCredentialsService)
        {
            _ldapUserManagementService = ldapUserManagementService;
        }

        /// <summary>
        /// Delete LDAP/Trino user using the LDAP user management service
        /// </summary>
        protected override async Task<bool> DeleteUserAsync(string? username, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(username))
                return false;

            var result = await _ldapUserManagementService.DeleteUserAsync(username);
            return result.Success;
        }
        protected override async Task<bool> CheckUserExistAsync(string username)
        {
            var result = await _ldapUserManagementService.UserExistsAsync(username);
            return result;
        }
    }
}
