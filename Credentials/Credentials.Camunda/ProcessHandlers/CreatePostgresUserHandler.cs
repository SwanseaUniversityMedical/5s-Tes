using System.Diagnostics;
using Credentials.Camunda.Models;
using Credentials.Camunda.Services;
using Credentials.Models.DbContexts;
using Credentials.Models.Models.Zeebe;
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
                _logger.LogInformation("RAW job.Variables: {Variables}", job.Variables);

                // Extract common variables
                var extraction = ExtractCredentials(job);
                submissionId = extraction.SubmissionId;
                parentProcessKey = extraction.ParentProcessKey;
                connectionTag = extraction.Variables.TryGetValue("tag", out var tagVal) ? tagVal?.ToString() ?? "postgres" : extraction.EnvList?.FirstOrDefault()?.tag ?? "postgres";

                // only use rows relevant to this connection
                List<CredentialsCamundaOutput> scopedEnvList = extraction.EnvList?.Where(x => string.Equals(x.tag, connectionTag, StringComparison.OrdinalIgnoreCase)).ToList() ?? new();

                // Refuse to provision unless this submission was approved by Agent.Api
                if (!await IsSubmissionApprovedAsync(extraction))
                {
                    await RecordErrorAsync(submissionId, parentProcessKey, processInstanceKey, connectionTag, "No matching approved submission record found");
                    return CreateStatusResponse("ERROR: Submission not approved.");
                }

                if (scopedEnvList.Count == 0)
                {
                    await RecordErrorAsync(submissionId, parentProcessKey, processInstanceKey, connectionTag,
                        "No credential information found in envList");
                    return CreateStatusResponse("ERROR: Missing credentials, cannot proceed.");
                }

                // Extract PostgreSQL-specific variables
                string? username = scopedEnvList
                    .Where(x => x.env.ToLower().Contains("username"))
                    .FirstOrDefault()?.value?.ToString();

                // Support multiple schema by finding all environment variables containing the phrase "schema"
                List<CredentialsCamundaOutput> schemaEntries = scopedEnvList.Where(x => x.env.Contains("schema", StringComparison.OrdinalIgnoreCase)).ToList();
                List<SchemaPermission> schemaPermissions = new();

                foreach (CredentialsCamundaOutput entry in schemaEntries) 
                {
                    // get the phrase we are using to identify this schema
                    string identifier = RemoveKeyword(entry.env, "schema");

                    // and find a matching permissions variable
                    string? rawSchemaPermissions = scopedEnvList.Where(x => x.env.Contains("permissions", StringComparison.OrdinalIgnoreCase))
                        .FirstOrDefault(x => string.Equals(RemoveKeyword(x.env, "permissions"), identifier, StringComparison.OrdinalIgnoreCase))?.value?.ToString();

                    schemaPermissions.Add(new()
                    {
                        SchemaName = entry.value,
                        Permissions = CreatePermissions(rawSchemaPermissions, connectionTag)
                    });
                }

                string? database = scopedEnvList
                    .Where(x => x.env.ToLower().Contains("database"))
                    .FirstOrDefault()?.value?.ToString();
                string? server = scopedEnvList
                    .Where(x => x.env.ToLower().Contains("server"))
                    .FirstOrDefault()?.value?.ToString();
                string? port = scopedEnvList
                    .Where(x => x.env.ToLower().Contains("port"))
                    .FirstOrDefault()?.value?.ToString();

                // Validate all required fields
                if (string.IsNullOrEmpty(username) || schemaPermissions.Count == 0 || schemaPermissions.Any(x => string.IsNullOrEmpty(x.SchemaName)) || string.IsNullOrEmpty(database) ||
                    string.IsNullOrEmpty(server) || string.IsNullOrEmpty(port) ||
                    string.IsNullOrEmpty(extraction.User) || string.IsNullOrEmpty(extraction.Project))
                {
                    await RecordErrorAsync(submissionId, parentProcessKey, processInstanceKey, connectionTag,
                        "Missing credentials; cannot proceed with Postgres user creation.");
                    return CreateStatusResponse("ERROR: Missing credentials, cannot proceed.");
                }

                // Generate password
                var password = GenerateSecurePassword();

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

                // Build credential data, removing any duplicate entries as vault requires unique keys
                var vaultEnvList = scopedEnvList.GroupBy(x => x.env, StringComparer.OrdinalIgnoreCase).Select(g => g.First()).ToList();
                var credentialData = BuildCredentialData(vaultEnvList, password);

                // Store in vault
                string vaultPath = $"{connectionTag}/{extraction.User}/{submissionId}/{extraction.Project}";
                if (!await StoreInVaultAsync(submissionId, parentProcessKey, processInstanceKey, vaultPath, credentialData, connectionTag))
                    return CreateStatusResponse("ERROR: Credential store in vault failed");

                // Call PostgreSQL service to create user
                var result = await _postgreSQLUserManagementService.CreateUserAsync(createUserRequest);
                if (!result.Success)
                {
                    // Remove the credential from Vault so that it isn't left orphaned in the event of an account creation failure.
                    await RollBackVaultCredentialAsync(vaultPath, connectionTag);
                    await RecordErrorAsync(submissionId, parentProcessKey, processInstanceKey, connectionTag,
                        $"Failed to create PostgreSQL user: {result.ErrorMessage}");
                    return CreateStatusResponse("ERROR: Failed credential creation");
                }

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
                await RecordErrorAsync(submissionId, parentProcessKey, processInstanceKey, connectionTag,
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

        /// <summary>
        /// Removes a given phrase from a string.
        /// </summary>
        /// <param name="input">The string we want to remove a keyword from.</param>
        /// <param name="keyword">The keyword we want to remove from the string.</param>
        /// <returns>Returns the string with the keyword removed.</returns>
        private string RemoveKeyword(string input, string keyword)
        {
            int index = input.IndexOf(keyword, StringComparison.OrdinalIgnoreCase);
            return index < 0 ? input : input.Remove(index, keyword.Length);
        }
    }
}
