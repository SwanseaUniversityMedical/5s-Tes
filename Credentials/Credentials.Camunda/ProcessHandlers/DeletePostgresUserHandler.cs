using Credentials.Camunda.Services;
using Zeebe.Client.Accelerator.Attributes;

namespace Credentials.Camunda.ProcessHandlers
{
    [JobType("delete-postgres-user")]
    public class DeletePostgresUserHandler : DeleteCredentialHandlerBase
    {
        private readonly IPostgreSQLUserManagementService _postgresUserManagementService;
        protected override string CredentialType => "postgres";

        public DeletePostgresUserHandler(
            ILogger<DeletePostgresUserHandler> logger,
            IPostgreSQLUserManagementService postgresUserManagementService,
            IVaultCredentialsService vaultCredentialsService,
            IEphemeralCredentialsService ephemeralCredentialsService)
            : base(logger, vaultCredentialsService, ephemeralCredentialsService)
        {
            _postgresUserManagementService = postgresUserManagementService;
        }

        /// <summary>
        /// Delete PostgreSQL user using the PostgreSQL user management service
        /// </summary>
        protected override async Task<bool> DeleteUserAsync(string? username, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(username))
                return false;

            var result = await _postgresUserManagementService.DropUserAsync(username);
            return result;
        }
        
        protected override async Task<bool> CheckUserExistAsync(string username)
        {
            var result = await _postgresUserManagementService.UserExistsAsync(username);
            return result;
        }
    }
}
