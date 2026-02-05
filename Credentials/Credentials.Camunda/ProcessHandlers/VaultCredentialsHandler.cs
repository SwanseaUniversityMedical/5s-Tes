using Hangfire;
using Serilog;
using System.Diagnostics;
using System.Text.Json;
using Credentials.Camunda.Services;
using Credentials.Models.DbContexts;
using Credentials.Models.Models;
using Zeebe.Client.Accelerator.Abstractions;
using Zeebe.Client.Accelerator.Attributes;

namespace Credentials.Camunda.ProcessHandlers
{
    [JobType("store-in-vault")]
    public class VaultCredentialsHandler : IAsyncZeebeWorkerWithResult<Dictionary<string, object>>
    {
        private readonly IVaultCredentialsService _vaultCredentialsService;
        private readonly ILogger<VaultCredentialsHandler> _logger;
        private readonly CredentialsDbContext _credentialsDbContext;        

        public VaultCredentialsHandler(IVaultCredentialsService vaultCredentialsService, ILogger<VaultCredentialsHandler> logger, CredentialsDbContext credentialsDbContext/*, IBackgroundJobClient backgroundJobClient*/)
        {
            _vaultCredentialsService = vaultCredentialsService;
            _logger = logger;
            _credentialsDbContext = credentialsDbContext;           
        }

        public async Task<Dictionary<string, object>> HandleJob(ZeebeJob job, CancellationToken cancellation)
        {
            var SW = new Stopwatch();
            SW.Start();

            _logger.LogDebug($"StoreInVaultHandler started for process instance {job.ProcessInstanceKey}");

            try
            {

                var variables = JsonSerializer.Deserialize<Dictionary<string, object>>(job.Variables);

                var envListJson = variables["envList"]?.ToString();
                var envList = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(envListJson);

                var usernameInfo = envList?.FirstOrDefault();
                var submissionInfo = envList?.LastOrDefault();

                var vaultPath = variables["vaultPath"]?.ToString();
                var credentialDataJson = variables["credentialData"]?.ToString();

                var submissionId = submissionInfo.ContainsKey("value") ? submissionInfo["value"]?.ToString()
                  : submissionInfo.ContainsKey("submissionId")
                  ? submissionInfo["submissionId"]?.ToString() : null;

                long? parentProcessKey = null;
                if (variables.TryGetValue("parentProcessKey", out var parentObj) && parentObj != null)
                {
                    if (parentObj is JsonElement el && el.ValueKind == JsonValueKind.Number && el.TryGetInt64(out var parsed))
                        parentProcessKey = parsed;
                    else if (long.TryParse(parentObj.ToString(), out var parsed2))
                        parentProcessKey = parsed2;
                }
                var processInstanceKey = job.ProcessInstanceKey;


                if (string.IsNullOrEmpty(vaultPath))
                {
                    var errorMsg = "Missing vaultPath for Vault storage";
                    _logger.LogError(errorMsg);
                    throw new Exception(errorMsg);
                }

                if (string.IsNullOrEmpty(credentialDataJson))
                {
                    var errorMsg = "Missing credentialData for Vault storage";
                    _logger.LogError(errorMsg);
                    throw new Exception(errorMsg);
                }

                if (string.IsNullOrEmpty(submissionId))
                {
                    var errorMsg = "Missing submissionId for Vault storage";
                    _logger.LogError(errorMsg);
                    throw new Exception(errorMsg);
                }

                Dictionary<string, object> credentialData;
                try
                {
                    var rawData = JsonSerializer.Deserialize<Dictionary<string, object>>(credentialDataJson);


                    credentialData = new Dictionary<string, object>();
                    foreach (var x in rawData)
                    {
                        if (x.Value is JsonElement element)
                        {
                            credentialData[x.Key] = element.ValueKind switch
                            {
                                JsonValueKind.String => element.GetString(),
                                JsonValueKind.Number => element.TryGetInt64(out var longVal) ? longVal : element.GetDouble(),
                                JsonValueKind.True => true,
                                JsonValueKind.False => false,
                                JsonValueKind.Null => null,
                                _ => element.GetRawText()
                            };
                        }
                        else
                        {
                            credentialData[x.Key] = x.Value;
                        }
                    }
                }
                catch (JsonException ex)
                {
                    var errorMsg = $"Invalid credentialData JSON format: {ex.Message}";
                    _logger.LogError(errorMsg);
                    throw new Exception(errorMsg);
                }

                if (credentialData == null || credentialData.Count == 0)
                {
                    var errorMsg = "credentialData is empty or null";
                    _logger.LogError(errorMsg);
                    throw new Exception(errorMsg);
                }


                var success = await _vaultCredentialsService.AddCredentialAsync(vaultPath, credentialData);

                if (!success)
                {
                    var errorMsg = $"Failed to store credential in Vault at path: {vaultPath}";
                    _logger.LogError(errorMsg);
                    throw new Exception(errorMsg);
                }

                await CreateCredentialsReadyMessage(submissionId, parentProcessKey, processInstanceKey, vaultPath);

                var outputVariables = new Dictionary<string, object>
                {
                    ["vaultPath"] = vaultPath,
                    ["vaultStorageStatus"] = "success",
                    ["storedAt"] = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),

                    /* Pass addiitonal variables if needed by next job, otherwise not needed */
                    ["credentialType"] = variables.ContainsKey("credentialType") ? variables["credentialType"] : "unknown",
                    ["project"] = variables.ContainsKey("project") ? variables["project"] : null,
                    ["userId"] = variables.ContainsKey("userId") ? variables["userId"] : null
                };

                _logger.LogInformation($"Successfully stored credential in Vault at path: {vaultPath}");

                SW.Stop();
                _logger.LogInformation($"StoreInVaultHandler took {SW.Elapsed.TotalSeconds} seconds");

                return outputVariables;
            }
            catch (Exception ex)
            {
                var errorMsg = $"Unexpected error in StoreInVaultHandler: {ex.Message}";
                _logger.LogError(ex, errorMsg);

                SW.Stop();
                _logger.LogInformation($"StoreInVaultHandler took {SW.Elapsed.TotalSeconds} seconds");

                throw;
            }
        }

        private async Task CreateCredentialsReadyMessage(string submissionId, long? parentProcessKey, long processInstanceKey, string vaultPath)
        {
            try
            {

                
                var submissionGuid = int.Parse(submissionId);
                var credType = ExtractCredType(vaultPath);
                var credReadyMessage = new EphemeralCredential
                {
                    SubmissionId = submissionGuid,
                    ParentProcessInstanceKey = parentProcessKey,
                    ProcessInstanceKey = processInstanceKey,
                    CreatedAt = DateTime.UtcNow,
                    IsProcessed = false,
                    VaultPath = vaultPath,
                    CredentialType = credType
                };

                _credentialsDbContext.EphemeralCredentials.Add(credReadyMessage);

                await _credentialsDbContext.SaveChangesAsync();                                
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error creating credentials ready message for submission: {submissionId}");
            }

        }

        private string ExtractCredType(string vaultPath)
        {
            try
            {
                var parts = vaultPath.Split('/');

                if (parts.Length > 0 && !string.IsNullOrEmpty(parts[0]))
                {
                    return parts[0].ToLower();
                }
                else
                {
                    Log.Warning("Could not extract cred type");
                    return "";
                }
            }
            catch (Exception ex)
            {
                Log.Error("Error extracting cred type from vault path");
                return "";
            }
        }

    }   
}
