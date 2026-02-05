using System.Diagnostics;
using System.Text;
using Credentials.Camunda.Models;
using Credentials.Camunda.Services;
using Credentials.Models.DbContexts;
using Zeebe.Client.Accelerator.Abstractions;
using Zeebe.Client.Accelerator.Attributes;

namespace Credentials.Camunda.ProcessHandlers
{
    [JobType("create-trino-user")]
    public class CreateTrinoUserHandler : CreateCredentialHandlerBase
    {
        private readonly ILdapUserManagementService _ldapUserManagementService;

        public CreateTrinoUserHandler(
            ILogger<CreateTrinoUserHandler> logger,
            ILdapUserManagementService ldapUserManagementService,
            IVaultCredentialsService vaultCredentialsService,
            CredentialsDbContext credentialsDbContext)
            : base(vaultCredentialsService, credentialsDbContext, logger)
        {
            _ldapUserManagementService = ldapUserManagementService;
        }

        public override async Task<Dictionary<string, object>> HandleJob(ZeebeJob job, CancellationToken cancellationToken)
        {
            var sw = Stopwatch.StartNew();
            _logger.LogDebug("CreateTrinoUserHandler started. processInstance={ProcessInstanceKey}", job.ProcessInstanceKey);

            string? submissionId = null;
            long? parentProcessKey = null;
            long processInstanceKey = job.ProcessInstanceKey;

            try
            {
                // Extract common variables
                var extraction = ExtractCredentials(job);
                submissionId = extraction.SubmissionId;
                parentProcessKey = extraction.ParentProcessKey;

                if (extraction.EnvList?.FirstOrDefault() == null)
                {
                    await RecordErrorAsync(submissionId, parentProcessKey, processInstanceKey, "trino",
                        "No credential information found in envList");
                    return CreateStatusResponse("ERROR: Missing credentials, cannot proceed.");
                }

                // Extract Trino-specific variables
                string? username = extraction.EnvList
                    .Where(x => x.env.ToLower().Contains("username"))
                    .FirstOrDefault()?.value?.ToString();

                // Validate all required fields
                if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(extraction.Project) ||
                    string.IsNullOrEmpty(extraction.User))
                {
                    await RecordErrorAsync(submissionId, parentProcessKey, processInstanceKey, "trino",
                        "Missing credentials; cannot proceed with Trino user creation.");
                    return CreateStatusResponse("ERROR: Missing credentials, cannot proceed.");
                }

                // Generate password
                var password = GenerateSecurePassword();

                // Create user request
                var createUserRequest = new CreateUserRequest
                {
                    Username = username,
                    Password = password,
                    CanLogin = true,
                    CanCreateDb = false,
                    CanCreateRole = false
                };

                // Call LDAP service to create user
                var result = await _ldapUserManagementService.CreateUserAsync(createUserRequest);

                if (!result.Success)
                {
                    await RecordErrorAsync(submissionId, parentProcessKey, processInstanceKey, "trino",
                        $"Failed to create Trino user: {result.ErrorMessage}");
                    return CreateStatusResponse("ERROR: Failed credential creation");
                }

                // Build credential data
                var credentialData = BuildCredentialData(extraction.EnvList, password);

                // Clean user DN and construct vault path
                var userId = CleanDnValue(extraction.User);
                string vaultPath = $"trino/{userId}/{submissionId}/{extraction.Project}";

                // Store in vault
                if (!await StoreInVaultAsync(submissionId, parentProcessKey, processInstanceKey, vaultPath, credentialData, "trino"))
                    return CreateStatusResponse("ERROR: Credential store in vault failed");

                // Record success
                await CreateCredentialsReadyMessageAsync(submissionId, parentProcessKey, processInstanceKey, vaultPath, "trino");

                _logger.LogInformation("Successfully created Trino user: {Username} for project: {Project}",
                    username, extraction.Project);
                return CreateStatusResponse($"OK: Trino user '{username}' created for project '{extraction.Project}'.");
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in CreateTrinoUserHandler. processInstance={ProcessInstanceKey}",
                    processInstanceKey);
                await RecordErrorAsync(submissionId, parentProcessKey, processInstanceKey, "trino",
                    $"Unexpected error: {ex.Message}");
                return CreateStatusResponse("Unexpected Error in Trino handler");
            }
            finally
            {
                if (sw.IsRunning) sw.Stop();
                _logger.LogInformation("CreateTrinoUserHandler took {Seconds} seconds", sw.Elapsed.TotalSeconds);
            }
        }

        /// <summary>
        /// Cleans LDAP DN (Distinguished Name) values by removing brackets, backslashes, and quotes
        /// </summary>
        private static string CleanDnValue(string value)
        {
            if (string.IsNullOrEmpty(value)) return value;

            var charsToRemove = new[] { '[', ']', '\\', '"' };
            var sb = new StringBuilder();
            foreach (var c in value)
            {
                if (!charsToRemove.Contains(c))
                {
                    sb.Append(c);
                }
            }
            return sb.ToString();
        }
    }
}
