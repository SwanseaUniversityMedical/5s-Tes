using System.Diagnostics;
using Credentials.Camunda.Models;
using Credentials.Camunda.Services;
using Credentials.Models.DbContexts;
using Zeebe.Client.Accelerator.Abstractions;
using Zeebe.Client.Accelerator.Attributes;

namespace Credentials.Camunda.ProcessHandlers
{
    [JobType("create-postgres-user")]
    public class CreatePostgresUserHandler : CreateCredentialHandlerBase
    {
        private readonly IPostgreSQLUserManagementService _postgreSQLUserManagementService;

        public CreatePostgresUserHandler(
            IPostgreSQLUserManagementService postgresSQLUserManagementService,
            ILogger<CreatePostgresUserHandler> logger,
            IVaultCredentialsService vaultCredentialsService,
            CredentialsDbContext credentialsDbContext)
            : base(vaultCredentialsService, credentialsDbContext, logger)
        {
            _postgreSQLUserManagementService = postgresSQLUserManagementService;
        }

        public override async Task<Dictionary<string, object>> HandleJob(ZeebeJob job, CancellationToken cancellationToken)
        {
            var sw = Stopwatch.StartNew();
            _logger.LogDebug("CreatePostgresUserHandler started. processInstance={ProcessInstanceKey}", job.ProcessInstanceKey);

            string? submissionId = null;
            long? parentProcessKey = null;
            long processInstanceKey = job.ProcessInstanceKey;
            string connectionTag = "postgres"; 

            try
            {
                // Extract common variables
                var extraction = ExtractCredentials(job);
                submissionId = extraction.SubmissionId;
                parentProcessKey = extraction.ParentProcessKey;

                if (extraction.EnvList?.FirstOrDefault() == null)
                {
                    connectionTag = extraction.EnvList.FirstOrDefault()?.tag ?? "postgres";
                    await RecordErrorAsync(submissionId, parentProcessKey, processInstanceKey, "postgres",
                        "No credential information found in envList");
                    return CreateStatusResponse("ERROR: Missing credentials, cannot proceed.");
                }

                // Extract PostgreSQL-specific variables
                string? username = extraction.EnvList
                    .Where(x => x.env.ToLower().Contains("username"))
                    .FirstOrDefault()?.value?.ToString();
                string? schemaName = extraction.EnvList
                    .FirstOrDefault(x =>
                    x.env.Equals("postgresSchema", StringComparison.OrdinalIgnoreCase))
                    ?.value?.ToString();

                string? database = extraction.EnvList
                    .Where(x => x.env.ToLower().Contains("database"))
                    .FirstOrDefault()?.value?.ToString();
                string? server = extraction.EnvList
                    .Where(x => x.env.ToLower().Contains("server"))
                    .FirstOrDefault()?.value?.ToString();
                string? port = extraction.EnvList
                    .Where(x => x.env.ToLower().Contains("port"))
                    .FirstOrDefault()?.value?.ToString();

                // Validate all required fields
                if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(schemaName) || string.IsNullOrEmpty(database) ||
                    string.IsNullOrEmpty(server) || string.IsNullOrEmpty(port) ||
                    string.IsNullOrEmpty(extraction.User) || string.IsNullOrEmpty(extraction.Project))
                {
                    await RecordErrorAsync(submissionId, parentProcessKey, processInstanceKey, connectionTag,
                        "Missing credentials; cannot proceed with Postgres user creation.");
                    return CreateStatusResponse("ERROR: Missing credentials, cannot proceed.");
                }

                string? rawPermissions = extraction.EnvList.FirstOrDefault(x => x.env.Equals("postgresPermissions", StringComparison.OrdinalIgnoreCase))?.value?.ToString();

                // Generate password
                var password = GenerateSecurePassword();

                var permissions = CreatePermissions(rawPermissions, connectionTag);

                // Create schema permissions for PostgreSQL
                var schemaPermissions = new List<SchemaPermission>
                {
                    new SchemaPermission
                    {
                        SchemaName = schemaName,
                        Permissions = permissions
                    }
                };

                // Create user request
                var createUserRequest = new CreateUserRequest
                {
                    Username = username,
                    Password = password,
                    Server = server,
                    Datasbasename = database,
                    Port = port,
                    SchemaPermissions = schemaPermissions
                };

                // Call PostgreSQL service to create user
                var result = await _postgreSQLUserManagementService.CreateUserAsync(createUserRequest);
                if (!result.Success)
                {
                    await RecordErrorAsync(submissionId, parentProcessKey, processInstanceKey, connectionTag,
                        $"Failed to create PostgreSQL user: {result.ErrorMessage}");
                    return CreateStatusResponse("ERROR: Failed credential creation");
                }

                // Build credential data
                var credentialData = BuildCredentialData(extraction.EnvList, password);

                // Store in vault
                string vaultPath = $"{connectionTag}/{extraction.User}/{submissionId}/{extraction.Project}";
                if (!await StoreInVaultAsync(submissionId, parentProcessKey, processInstanceKey, vaultPath, credentialData, connectionTag))
                    return CreateStatusResponse("ERROR: Credential store in vault failed");

                // Record success
                await CreateCredentialsReadyMessageAsync(submissionId, parentProcessKey, processInstanceKey, vaultPath, connectionTag);

                _logger.LogInformation("Successfully created PostgreSQL user: {Username} for project: {Project}",
                    username, extraction.Project);
                return CreateStatusResponse($"OK: PostgreSQL user '{username}' created for project '{extraction.Project}'.");
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in CreatePostgresUserHandler. processInstance={ProcessInstanceKey}",
                    processInstanceKey);
                await RecordErrorAsync(submissionId, parentProcessKey, processInstanceKey, "postgres",
                    $"Unexpected error: {ex.Message}");
                return CreateStatusResponse("Unexpected Error in Postgres handler");
            }
            finally
            {
                if (sw.IsRunning) sw.Stop();
                _logger.LogInformation("CreatePostgresUserHandler took {Seconds} seconds", sw.Elapsed.TotalSeconds);
            }
        }

        private DatabasePermissions CreatePermissions(string rawPermissions, string connectionTag) 
        {
            DatabasePermissions permissions = DatabasePermissions.Read;

            if (!string.IsNullOrWhiteSpace(rawPermissions))
            {
                var parsedPermissions = DatabasePermissions.None;
                foreach (var part in rawPermissions.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    if (Enum.TryParse<DatabasePermissions>(part, ignoreCase: true, out var parsedFlag))
                    {
                        parsedPermissions |= parsedFlag;
                    }
                    else
                    {
                        _logger.LogWarning("Unrecognised postgres permission '{Permission}' for tag {Tag}, ignoring", part, connectionTag);
                    }
                }
                if (parsedPermissions != DatabasePermissions.None)
                {
                    permissions = parsedPermissions;
                }
            }

            return permissions;
        }
    }
}
