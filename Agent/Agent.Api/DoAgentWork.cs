using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Agent.Api.Constants;
using Agent.Api.Models;
using Agent.Api.Repositories;
using Agent.Api.Repositories.DbContexts;
using Agent.Api.Services;
using Credentials.Models.DbContexts;
using Credentials.Models.Models;
using EasyNetQ;
using FiveSafesTes.Core.Models;
using FiveSafesTes.Core.Models.Enums;
using FiveSafesTes.Core.Models.Settings;
using FiveSafesTes.Core.Models.Tes;
using FiveSafesTes.Core.Models.ViewModels;
using FiveSafesTes.Core.Rabbit;
using FiveSafesTes.Core.Services;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.FeatureManagement;
using Newtonsoft.Json;
using Serilog;

namespace Agent.Api
{
    public interface IDoAgentWork
    {
        [AutomaticRetry(Attempts = 0)]
        Task Execute();

        Task CheckTES(string taskID, int subId, int projectId, int userId, string tesId, string outputBucket,
            string NameTes);

        void ClearJob(string jobname);
    }

    public class DoAgentWork : IDoAgentWork
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ApplicationDbContext _dbContext;
        private readonly ISubmissionHelper _subHelper;
        private readonly IMinioSubHelper _minioSubHelper;
        private readonly IMinioTreHelper _minioTreHelper;
        private readonly IProjectS3SubHelperFactory _projectS3SubHelperFactory;
        private readonly IHasuraAuthenticationService _hasuraAuthenticationService;
        private readonly IDareClientWithoutTokenHelper _dareHelper;
        private readonly AgentSettings _AgentSettings;
        private readonly MinioSettings _minioSettings;
        private readonly IKeyCloakService _keyCloakService;
        private readonly TreKeyCloakSettings _treKeyCloakSettings;
        private readonly IEncDecHelper _encDecHelper;
        private readonly IFeatureManager _features;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly CredentialsDbContext _credsDbContext;
        private readonly IVaultCredentialsService _vaultService;
        private readonly IConfiguration _config;
        // Used to start Camunda process instances directly via Zeebe gRPC, replacing the previous HTTP calls to CredentialsController
        private readonly Credentials.Models.Services.IServicedZeebeClient _zeebeClient;
        private readonly IImageCodeResolver _imageCodeResolver;
        private readonly IOptionsMonitor<TreOnboardingConfig> _onboardingConfig;


        public DoAgentWork(IServiceProvider serviceProvider,
            ApplicationDbContext dbContext,
            ISubmissionHelper subHelper,
            IMinioTreHelper minioTreHelper,
            IMinioSubHelper minioSubHelper,
            IProjectS3SubHelperFactory projectS3SubHelperFactory,
            IHasuraAuthenticationService hasuraAuthenticationService,
            IDareClientWithoutTokenHelper dareHelper,
            AgentSettings AgentSettings,
            MinioSettings minioSettings,
            IKeyCloakService keyCloakService,
            IOptions<TreKeyCloakSettings> treKeyCloakSettings,
            IEncDecHelper encDecHelper,
            IFeatureManager features,
            IHttpClientFactory httpClientFactory,
            CredentialsDbContext credsDbContext,
            IVaultCredentialsService vaultService,
            IConfiguration config,
            Credentials.Models.Services.IServicedZeebeClient zeebeClient,
            IImageCodeResolver imageCodeResolver,
            IOptionsMonitor<TreOnboardingConfig> configSettings
        )
        {
            _serviceProvider = serviceProvider;
            _dbContext = dbContext;
            _subHelper = subHelper;

            _minioTreHelper = minioTreHelper;
            _minioSubHelper = minioSubHelper;
            _projectS3SubHelperFactory = projectS3SubHelperFactory;

            _serviceProvider = serviceProvider;
            _dbContext = dbContext;
            _subHelper = subHelper;

            _hasuraAuthenticationService = hasuraAuthenticationService;
            _dareHelper = dareHelper;
            _AgentSettings = AgentSettings;
            _minioSettings = minioSettings;

            _minioTreHelper = minioTreHelper;
            _minioSubHelper = minioSubHelper;

            _keyCloakService = keyCloakService;
            _treKeyCloakSettings = treKeyCloakSettings.Value;
            _encDecHelper = encDecHelper;
            _features = features;
            _httpClientFactory = httpClientFactory;
            _credsDbContext = credsDbContext;
            _vaultService = vaultService;
            _config = config;
            _zeebeClient = zeebeClient;
            _imageCodeResolver = imageCodeResolver;
            _onboardingConfig = configSettings;
        }

        public string CreateTesk(string jsonContent, int subId, int projectId, int userId, string tesId,
            string outputBucket, string Tesname)
        {
            Log.Information("{Function} {jsonContent} running CreateTESK ", "CreateTesk", jsonContent);

            HttpClientHandler handler = new HttpClientHandler();

            if (_AgentSettings.Proxy)
            {
                handler = new HttpClientHandler
                {
                    Proxy = new WebProxy(_AgentSettings.ProxyAddresURL, true), // Replace with your proxy server URL
                    UseProxy = _AgentSettings.Proxy,
                };
            }


            using var httpClient = new HttpClient(handler);
            // Define the URL for the POST request
            string apiUrl = _AgentSettings.TESKAPIURL;

            // Create a HttpRequestMessage with the HTTP method set to POST
            var request = new HttpRequestMessage(HttpMethod.Post, apiUrl);

            // Set the headers
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            request.Headers.TryAddWithoutValidation("Content-Type", "application/json");

            // Attach the JSON string to the request's content
            request.Content = new StringContent(jsonContent, Encoding.UTF8, "application/json");


            // Send the POST request
            HttpResponseMessage response = httpClient.SendAsync(request).Result;

            // Check the response status
            if (response.IsSuccessStatusCode)
            {
                string responseBody = response.Content.ReadAsStringAsync().Result;
                Log.Information("{Function} Request successful. Response: {Response}", "CreateTesk", responseBody);
                Console.WriteLine("Request successful. Response:");
                Console.WriteLine(responseBody);
                Log.Information("Request successful. Response: {response}", responseBody);


                var responseObj = JsonConvert.DeserializeObject<ResponseModel>(responseBody);
                string id = responseObj.id;


                RecurringJob.AddOrUpdate<IDoAgentWork>(id,
                    a => a.CheckTES(id, subId, projectId, userId, tesId, outputBucket, Tesname),
                    Cron.Minutely());


                _dbContext.Add(new TeskAudit() { message = jsonContent, teskid = tesId, subid = subId.ToString() });
                _dbContext.SaveChanges();


                return id;
            }

            try
            {
                string responseBody = response.Content.ReadAsStringAsync().Result;
                Log.Error("{Function} Request failed with status code: {Code} {responseBody}", "CreateTESK",
                    response.StatusCode, responseBody);
            }
            catch (Exception ex)
            {
                Log.Error("{Function} Request failed with status code: {Code}", "CreateTESK", response.StatusCode);
            }


            return "";
        }


        class ResponseModel
        {
            public string id { get; set; }
        }

        public async Task CheckTES(string taskID, int subId, int projectId, int userId, string tesId,
            string outputBucket, string NameTes)
        {
            try
            {
                var treName = _config["TreName"];

                Log.Information("{Function} {TreName} checking TES task {TaskId} (TES {TesId}) for sub {SubId}",
                    "CheckTES", treName, taskID, tesId, subId);
                string url = _AgentSettings.TESKAPIURL + "/" + taskID + "?view=BASIC";

                HttpClientHandler handler = new HttpClientHandler();
                // Getting project name
                var projectName =
                    _dbContext.Projects.FirstOrDefault(p => p.SubmissionProjectId == projectId)
                        ?.SubmissionProjectName;
                if (projectName is null)
                {
                    Log.Error("{Function} Could not find project name for projectId {ProjectId}", "CheckTES",
                        projectId);
                }

                if (_AgentSettings.Proxy)
                {
                    handler = new HttpClientHandler
                    {
                        Proxy = new WebProxy(_AgentSettings.ProxyAddresURL, true), // Replace with your proxy server URL
                        UseProxy = _AgentSettings.Proxy,
                    };
                }

                using (HttpClient client = new HttpClient(handler))
                {
                    HttpResponseMessage response = client.GetAsync(url).Result;
                    Log.Information("{Function} {TreName} TESK responded {State} for sub {SubId} (task {TaskId})",
                        "CheckTES", treName, response.StatusCode, subId, taskID);

                    if (response.IsSuccessStatusCode)
                    {
                        string content = response.Content.ReadAsStringAsync().Result;


                        TESKstatus status = JsonConvert.DeserializeObject<TESKstatus>(content);

                        var shouldReport = false;

                        var fromDatabase = (_dbContext.TESK_Status.Where(x => x.id == status.id)).FirstOrDefault();

                        if (fromDatabase is null)
                        {
                            shouldReport = true;
                            _dbContext.Add(status);
                        }
                        else
                        {
                            if (fromDatabase.state != status.state)
                            {
                                shouldReport = true;
                                fromDatabase.state = status.state;
                                _dbContext.Update(fromDatabase);
                            }
                        }

                        _dbContext.SaveChanges();
                        Log.Information("{Function} shouldReport {shouldReport} status {status}", "CheckTES",
                            shouldReport, status.state);
                        if (shouldReport || (status.state == "COMPLETE" || status.state == "EXECUTOR_ERROR" ||
                                             status.state == "SYSTEM_ERROR"))
                        {
                            Log.Information("{Function} *** status change *** {State} {name} {description}", "CheckTES",
                                status.state, status.name, status.description);


                            // send update
                            using (var scope = _serviceProvider.CreateScope())
                            {
                                TokenToExpire Token = null;
                                var statusMessage = StatusType.TransferredToPod;
                                switch (status.state)
                                {
                                    case "QUEUED":
                                        statusMessage = StatusType.TransferredToPod;
                                        break;
                                    case "RUNNING":
                                        statusMessage = StatusType.PodProcessing;
                                        break;
                                    case "COMPLETE":
                                        statusMessage = StatusType.PodProcessingComplete;

                                        Token = _dbContext.TokensToExpire.FirstOrDefault(x => x.SubId == subId);
                                        Log.Information("{Function} *** COMPLETE remove Token *** {Token} ",
                                            "CheckTES", Token);

                                        try
                                        {
                                            await TriggerRevokeCredentialsAsync(subId, projectName, userId, 0);
                                        }
                                        catch (Exception ex)
                                        {
                                            Log.Error(ex, "Failed to trigger revoke credentials for submission {SubId}",
                                                subId);
                                        }

                                        if (Token != null)
                                        {
                                            _dbContext.TokensToExpire.Remove(Token);
                                            _hasuraAuthenticationService.ExpirerToken(Token.Token);
                                        }

                                        _dbContext.SaveChanges();
                                        break;
                                    case "EXECUTOR_ERROR":
                                        statusMessage = StatusType.Cancelled;
                                        Token = _dbContext.TokensToExpire.FirstOrDefault(x => x.SubId == subId);
                                        Log.Information("{Function} *** EXECUTOR_ERROR remove Token *** {Token} ",
                                            "CheckTES", Token);
                                        try
                                        {
                                            await TriggerRevokeCredentialsAsync(subId, projectName, userId, 0);
                                        }
                                        catch (Exception ex)
                                        {
                                            Log.Error(ex, "Failed to trigger revoke credentials for submission {SubId}",
                                                subId);
                                        }

                                        if (Token != null)
                                        {
                                            _dbContext.TokensToExpire.Remove(Token);
                                            _hasuraAuthenticationService.ExpirerToken(Token.Token);
                                        }

                                        _dbContext.SaveChanges();

                                        break;
                                    case "SYSTEM_ERROR":
                                        statusMessage = StatusType.Cancelled;
                                        Token = _dbContext.TokensToExpire.FirstOrDefault(x => x.SubId == subId);
                                        Log.Information("{Function} *** SYSTEM_ERROR remove Token *** {Token} ",
                                            "CheckTES", Token);
                                        try
                                        {
                                            await TriggerRevokeCredentialsAsync(subId, projectName, userId, 0);
                                        }
                                        catch (Exception ex)
                                        {
                                            Log.Error(ex, "Failed to trigger revoke credentials for submission {SubId}",
                                                subId);
                                        }

                                        if (Token != null)
                                        {
                                            _dbContext.TokensToExpire.Remove(Token);
                                            _hasuraAuthenticationService.ExpirerToken(Token.Token);
                                        }

                                        _dbContext.SaveChanges();

                                        break;
                                }

                                APIReturn? result = null;


                                if (status.state == "COMPLETE")
                                {
                                    Log.Information(
                                        $"  CloseSubmissionForTre with status.state subId {subId.ToString()} == COMPLETE ");
                                    try
                                    {
                                        result = _subHelper.CloseSubmissionForTre(subId.ToString(),
                                            StatusType.DataOutRequested, "", "");
                                    }
                                    catch (Exception ex)
                                    {
                                        Log.Error(ex.ToString());
                                    }

                                    ClearJob(taskID);
                                }
                                else if (status.state == "EXECUTOR_ERROR" || status.state == "SYSTEM_ERROR")
                                {
                                    // TES task failed. Close as Failed (a valid terminal status) and attach
                                    // the TES state (EXECUTOR_ERROR / SYSTEM_ERROR) as the reason.
                                    var failureReason = status.state;

                                    Log.Error(
                                        "{Function} TES task failed for sub {SubId} (task {TaskId}), state {State}: {Reason}",
                                        "CheckTES", subId, taskID, status.state, failureReason);
                                    try
                                    {
                                        result = _subHelper.CloseSubmissionForTre(subId.ToString(),
                                            StatusType.Failed, failureReason, "");
                                    }
                                    catch (Exception ex)
                                    {
                                        Log.Error(ex.ToString());
                                    }

                                    ClearJob(taskID);
                                }
                            }

                            Log.Information($" Checking status ");
                            // are we done ?
                            if (status.state == "COMPLETE")
                            {
                                Log.Information(
                                    "status.state == COMPLETE");

                                ClearJob(taskID);
                                var outputBucketGood = outputBucket.Replace(_AgentSettings.TESKOutputBucketPrefix, "");
                                var data = await _minioTreHelper.GetFilesInBucket(outputBucketGood, $"{subId}");

                                if (data != null)
                                {
                                    var files = new List<string>();

                                    foreach (var s3Object in data.S3Objects) //TODO is this right?
                                    {
                                        Log.Information("{Function} *** added file from outputBucket *** {file} ",
                                            "CheckTES", s3Object.Key);
                                        files.Add(s3Object.Key);
                                    }

                                    _subHelper.UpdateStatusForTre(subId.ToString(), StatusType.DataOutRequested, "");
                                    Log.Information($"  FilesReadyForReview files {files.Count} ");
                                    if (files.Count == 0)
                                    {
                                        _subHelper.UpdateStatusForTre(subId.ToString(), StatusType.Complete,
                                            " Complete - No files to review ");
                                        return;
                                    }

                                    _subHelper.FilesReadyForReview(new ReviewFiles()
                                    {
                                        SubId = subId.ToString(),
                                        Files = files,
                                        tesId = tesId.ToString(),
                                        Name = NameTes
                                    }, outputBucketGood);
                                }
                                else
                                {
                                    _subHelper.UpdateStatusForTre(subId.ToString(), StatusType.Failed,
                                            " Failed to get files in bucket. ");
                                    return;
                                }
                            }
                        }
                        else
                            Log.Information("{Function} {TreName} no status change for sub {SubId} (state {State})",
                                "CheckTES", treName, subId, status.state);
                    }
                    else
                    {
                        Log.Error("{Function} {TreName} TESK poll failed for sub {SubId} — request {Url} returned {Code}",
                            "CheckTES", treName, subId, url, response.StatusCode);
                    }
                }
            }
            catch (Exception e)
            {
                Log.Error(e.ToString());
            }
        }

        // Method executed upon hangfire job
        public async Task Execute()
        {
            if (!_onboardingConfig.CurrentValue.IsConfigurationImported) return;

            var treName = _config["TreName"];

            Log.Information("{Function} {TreName} DoAgentWork running", "Execute", treName);
            // control use of dependency injection
            using (var scope = _serviceProvider.CreateScope())
            {
                // OPTIONS
                var useRabbit = _AgentSettings.UseRabbit;
                var useTESK = _AgentSettings.UseTESK;

                Log.Information("{Function} {TreName} useRabbit {useRabbit}", "Execute", treName, useRabbit);
                Log.Information("{Function} {TreName} useTESK {useTESK}", "Execute", treName, useTESK);

                var cancelsubprojs = _subHelper.GetRequestCancelSubsForTre();
                if (cancelsubprojs != null)
                {
                    foreach (var cancelsubproj in cancelsubprojs)
                    {
                        // Carry whatever reason put the submission into RequestCancellation
                        // through to the terminal status. Passing an empty description here
                        // overwrote it, so a credential failure ended up as a bare "Cancelled"
                        // with nothing to explain it.
                        var cancelReason = cancelsubproj.StatusDescription ?? string.Empty;

                        _subHelper.UpdateStatusForTre(cancelsubproj.Id.ToString(), StatusType.CancellationRequestSent,
                            cancelReason);
                        //TODO Do we need to call Hutch or other stuff to cancel and do other cancel stuff
                        _subHelper.CloseSubmissionForTre(cancelsubproj.Id.ToString(), StatusType.Cancelled,
                            cancelReason, "");
                    }
                }

                // Get list of submissions
                List<Submission> listOfSubmissions;

                Log.Information("{Function} {TreName} is scanning for submissions...", "Execute", treName);

                try
                {
                    listOfSubmissions = _subHelper.GetWaitingSubmissionForTre();
                }
                catch (Exception e)
                {
                    Log.Error(e, "{Function} {TreName} error getting submissions", "Execute", treName);

                    throw;
                }


                Log.Information("{Function} {TreName} - Submissions found: {listOfSubmissions}", "Execute",
                    treName, listOfSubmissions?.Count);
                foreach (var aSubmission in listOfSubmissions)
                {
                    try
                    {
                        Log.Information("{Function} {TreName} processing submission: {submission}", "Execute",
                            treName, aSubmission.Id);

                        // Check user is allowed on the project
                        if (!_subHelper.IsUserApprovedOnProject(aSubmission.Project.Id, aSubmission.SubmittedBy.Id))
                        {
                            Log.Error(
                                "{Function} {TreName} User {UserID}/project {ProjectId} is not valid for this submission {submission}",
                                "Execute", treName,
                                aSubmission.SubmittedBy.Id, aSubmission.Project.Id, aSubmission);
                            // record error with submission layer
                            var result =
                                _subHelper.UpdateStatusForTre(aSubmission.Id.ToString(), StatusType.InvalidUser, "");
                            result = _subHelper.CloseSubmissionForTre(aSubmission.Id.ToString(), StatusType.Failed, "",
                                "");
                        }


                        else
                        {
                            // Submission picked up by this TRE — surface it as a step under Tre Layer
                            // Processing. Guarded on the queue status so it's emitted once on first
                            // pickup, not re-emitted every scan cycle while we wait on credentials.
                            if (aSubmission.Status == StatusType.WaitingForAgentToTransfer)
                            {
                                _subHelper.UpdateStatusForTre(aSubmission.Id.ToString(),
                                    StatusType.AgentTransferringToPod, "");
                            }

                            SubmissionCredentials credentials = new SubmissionCredentials();

                            if (await _features.IsEnabledAsync(FeatureFlags.EphemeralCredentials))
                            {
                                // Entering credential provisioning — surface it as a step. Guarded so it
                                // is emitted once, not on every re-pick while credentials are still pending.
                                if (aSubmission.Status != StatusType.ProcessingCredentials)
                                {
                                    _subHelper.UpdateStatusForTre(aSubmission.Id.ToString(),
                                        StatusType.ProcessingCredentials, "");
                                }

                                var credsForSubmission = await _credsDbContext.EphemeralCredentials
                                    .Where(c => c.SubmissionId == aSubmission.Id).ToListAsync();

                                //This is the check to see if the start credentials were triggered for this submission
                                bool alreadyTriggered = credsForSubmission.Any();

                                // This is the check to see if the credentials were already fetched and processed for this submission
                                bool alreadyProcessed = credsForSubmission.Any(c => c.IsProcessed == true);

                                if (!alreadyTriggered)
                                {
                                    try
                                    {
                                        var project = aSubmission.Project.Name;

                                        // Record this submission in the db so it can be verified by Credentials.Camunda.
                                        // ... but don't create a new one if a record exists already for this submission
                                        var existingApproval = await _credsDbContext.ApprovedSubmissions.FirstOrDefaultAsync(a => a.SubmissionId == aSubmission.Id);

                                        if (existingApproval == null)
                                        {
                                            _credsDbContext.ApprovedSubmissions.Add(new()
                                            {
                                                SubmissionId = aSubmission.Id,
                                                Project = project,
                                                UserId = aSubmission.SubmittedBy.Id,
                                                CreatedAt = DateTime.UtcNow
                                            });

                                            await _credsDbContext.SaveChangesAsync();
                                        }
                                        // Ephemeral S3 credentials are scoped to the project's TRE
                                        // buckets, so pass them on the kickoff payload for the DMN to
                                        // emit into the s3 credential branch. The workload-facing S3
                                        // endpoint is taken from config (MinioTRESettings) rather than
                                        // hardcoded, so it travels with the credential too.
                                        var treProjectForCreds = _dbContext.Projects
                                            .FirstOrDefault(x => x.SubmissionProjectId == aSubmission.Project.Id);

                                        await TriggerStartCredentialsAsync(aSubmission.Id, project,
                                            aSubmission.SubmittedBy.Id,
                                            treProjectForCreds?.SubmissionBucketTre,
                                            treProjectForCreds?.OutputBucketTre,
                                            _config["MinioTRESettings:Url"]);
                                        Log.Information("Triggered credentials for submission {SubId}", aSubmission.Id);
                                    }
                                    catch (Exception ex)
                                    {
                                        Log.Error(ex, "Failed to trigger credentials for Sub {Sub}", aSubmission.Id);
                                        continue;
                                    }
                                }

                                if (!alreadyProcessed)
                                {
                                    Log.Information($"Fetching ephemeral credential data from Camunda");

                                    var creds = await GetLatestCreds(aSubmission.Id);

                                    if (creds.Count == 0)
                                    {
                                        Log.Information(
                                            "No credentials yet for submission {SubId}. Will retry inside same run.",
                                            aSubmission.Id);

                                        var retryStopwatch = Stopwatch.StartNew();
                                        while (retryStopwatch.Elapsed < TimeSpan.FromSeconds(60))
                                        {
                                            await Task.Delay(2000);
                                            creds = await GetLatestCreds(aSubmission.Id);

                                            if (creds.Count > 0)
                                                break;
                                        }

                                        if (creds.Count == 0)
                                        {
                                            Log.Information(
                                                "Still no credentials after retry window. Skipping until next Hangfire cycle.");
                                            continue;
                                        }
                                    }

                                    //This check is to avoid duplicate runs for the same submission ID
                                    var parentKey = creds.Select(c => c.ParentProcessInstanceKey)
                                        .FirstOrDefault(k => k.HasValue && k.Value > 0);

                                    if (!parentKey.HasValue)
                                    {
                                        Log.Information(
                                            "No parent processInstanceKey for submission {SubId}. Skipping this cycle.",
                                            aSubmission.Id);
                                        continue;
                                    }

                                    var credsRowforParentKey = await _credsDbContext.EphemeralCredentials
                                        .Where(e => e.SubmissionId == aSubmission.Id &&
                                                    e.ParentProcessInstanceKey == parentKey && e.IsProcessed != true)
                                        .OrderByDescending(e => e.CreatedAt).ToListAsync();

                                    bool anyErrored =
                                        credsRowforParentKey.Any(c => c.SuccessStatus == SuccessStatus.Error);
                                    bool allSucceeded =
                                        credsRowforParentKey.All(c => c.SuccessStatus == SuccessStatus.Success);

                                    if (anyErrored)
                                    {
                                        // Name the handler that failed and why. "Credential process
                                        // failed" on its own leaves a researcher, and whoever they
                                        // ask, with nothing to go on.
                                        var failures = credsRowforParentKey
                                            .Where(c => c.SuccessStatus == SuccessStatus.Error
                                                        && !string.IsNullOrWhiteSpace(c.ErrorMessage))
                                            .Select(c => $"{c.CredentialType}: {c.ErrorMessage}")
                                            .Distinct()
                                            .ToList();

                                        var reason = failures.Count > 0
                                            ? "Credential process failed - " + string.Join("; ", failures)
                                            : "Credential process failed";

                                        Log.Error("{Reason} for submission {SubId}.", reason, aSubmission.Id);

                                        var latestRow = creds.First();
                                        latestRow.ErrorMessage = reason;
                                        await _credsDbContext.SaveChangesAsync();

                                        // Handlers run in sequence, so an failure part-way leaves the
                                        // earlier ones' accounts and vault paths behind. Nothing else
                                        // collects them: there is no credential sweeper, nothing scans
                                        // for unexpired rows, and Vault has no expiry on the KV mount.
                                        // Revoke immediately rather than after the usual delay.
                                        try
                                        {
                                            await TriggerRevokeCredentialsAsync(aSubmission.Id,
                                                aSubmission.Project.Name, aSubmission.SubmittedBy.Id, 0);
                                        }
                                        catch (Exception ex)
                                        {
                                            Log.Error(ex,
                                                "Failed to revoke credentials after a failed credential process for submission {SubId}",
                                                aSubmission.Id);
                                        }

                                        _subHelper.UpdateStatusForTre(aSubmission.Id.ToString(),
                                            StatusType.RequestCancellation, reason);
                                        continue;
                                    }

                                    if (!allSucceeded)
                                    {
                                        Log.Information(
                                            "Credential process still running for submission , Will retry next run.");
                                        continue;
                                    }

                                    Log.Information(
                                        "All credential handlers succeeded for submission {SubId}. Fetching credentials.",
                                        aSubmission.Id);
                                    credentials =
                                        await WaitForAndFetchCredentialsAsync(aSubmission.Id, TimeSpan.FromMinutes(10));

                                    if (credentials == null || credentials.Count == 0)
                                    {
                                        var errorMsg = $"No credentials found in Vault for submission {aSubmission.Id}";
                                        Log.Error(errorMsg);

                                        var latestRow = creds.First();
                                        latestRow.ErrorMessage = errorMsg;
                                        await _credsDbContext.SaveChangesAsync();

                                        _subHelper.UpdateStatusForTre(aSubmission.Id.ToString(),
                                            StatusType.RequestCancellation, errorMsg);
                                        continue;
                                    }

                                    Log.Information(
                                        $"Successfully obtained {credentials.Count} credentials for submission {aSubmission.Id}");
                                    await _credsDbContext.SaveChangesAsync();

                                    try
                                    {
                                        await TriggerRevokeCredentialsAsync(aSubmission.Id, aSubmission.Project.Name,
                                            aSubmission.SubmittedBy.Id, 1);
                                    }
                                    catch (Exception ex)
                                    {
                                        Log.Error(ex, "Failed to trigger revoke credentials for submission {SubId}",
                                            aSubmission.Id);
                                    }
                                }
                                else
                                {
                                    Log.Information("Submission {SubId} already fully processed. Skipping.",
                                        aSubmission.Id);
                                    continue;
                                }
                            }

                            else
                            {
                                Log.Information("EphemeralCredentials feature is disabled.");
                            }

                            // The TES message
                            var tesMessage = JsonConvert.DeserializeObject<TesTask>(aSubmission.TesJson);
                            var processedOK = true;

                            if (tesMessage is not null)
                            {
                                TesVolumeHelper.ApplyDefaultTmpVolume(tesMessage);
                            }

                            // **************  SEND TO RABBIT
                            if (useRabbit)
                            {
                                try
                                {
                                    // Not ideal to create each time around the loop but ???
                                    IBus rabbit = scope.ServiceProvider.GetRequiredService<IBus>();
                                    EasyNetQ.Topology.Exchange exchangeObject =
                                        rabbit.Advanced.ExchangeDeclare(ExchangeConstants.Submission, "topic");
                                    rabbit.Advanced.Publish(exchangeObject, RoutingConstants.ProcessSub, false,
                                        new Message<TesTask>(tesMessage));
                                }
                                catch (Exception e)
                                {
                                    Log.Error(e, "{Function} {TreName} send rabbit failed for sub {SubId}", "Execute",
                                        treName, aSubmission.Id);
                                    processedOK = false;
                                }
                            }

                            // **************  SEND TO TESK
                            if (useTESK)
                            {
                                Log.Information("{Function} {TreName} sending submission {SubId} to TESK", "Execute",
                                    treName, aSubmission.Id);
                                var arr = new HttpClient();
                                var Token = "";

                                var role = aSubmission.Project.Name; //TODO Check

                                if (await _features.IsEnabledAsync(FeatureFlags.GenerateAccounts) &&
                                    await _features.IsEnabledAsync(FeatureFlags.SqlAndNotGraphQl))
                                {
                                    var Acount = _dbContext.ProjectAcount.FirstOrDefault(x =>
                                        x.Name == aSubmission.Project.Name + aSubmission.SubmittedBy.Name);

                                    var TokenIN = await _keyCloakService.GenAccessTokenSimple(Acount.Name,
                                        _encDecHelper.Decrypt(Acount.Pass), _treKeyCloakSettings.TokenRefreshSeconds);

                                    Token = TokenIN.access_token;
                                }

                                if (!await _features.IsEnabledAsync(FeatureFlags.SqlAndNotGraphQl))
                                {
                                    Token = _hasuraAuthenticationService.GetNewToken(role);
                                }

                                var projectId = aSubmission.Project.Id;

                                var OutputBucket = _AgentSettings.TESKOutputBucketPrefix + _dbContext.Projects
                                    .First(x => x.SubmissionProjectId == projectId)
                                    .OutputBucketTre; //TODO Check, Projects not getting The synchronised Properly 


                                //it need the file name?? (key-name)

                                if (tesMessage.Outputs == null)
                                {
                                    tesMessage.Outputs = new List<TesOutput> { };
                                }


                                //S3://bucket-name/key-name
                                foreach (var output in tesMessage.Outputs)
                                {
                                    output.Url = OutputBucket + $"/{aSubmission.Id}";
                                }


                                var InputBucket = _dbContext.Projects
                                    .First(x => x.SubmissionProjectId == projectId)
                                    .SubmissionBucketTre;

                                var bucket = _subHelper.GetOutputBucketGutsSub(aSubmission.Id.ToString(), true);

                                TesInput MandatoryInput = null;

                                if (tesMessage.Inputs == null)
                                {
                                    tesMessage.Inputs = new List<TesInput>();
                                }

                                if (string.IsNullOrEmpty(_AgentSettings.MandatoryInput) == false)
                                {
                                    MandatoryInput =
                                        JsonConvert.DeserializeObject<TesInput>(_AgentSettings.MandatoryInput);
                                    tesMessage.Inputs.Add(MandatoryInput);
                                }


                                var Files = await _minioTreHelper.GetFilesInBucket(InputBucket);

                                foreach (var input in tesMessage.Inputs)
                                {
                                    input.Path = input.Path.Replace("..", "");


                                    if (input != MandatoryInput)
                                    {
                                        input.Url = "s3://" + InputBucket + "/data" + input.Path;
                                    }
                                    else
                                    {
                                        input.Url = "s3://" + InputBucket + input.Path;
                                    }


                                    if (string.IsNullOrEmpty(input.Name))
                                    {
                                        if (input.Path.Contains("/"))
                                        {
                                            input.Name = input.Path.Split('/')[^1];
                                        }
                                    }


                                    if (input == MandatoryInput)
                                    {
                                        continue;
                                    }


                                    var CleanedIntput = input.Path;
                                    input.Path = "/data" + input.Path;
                                    if (CleanedIntput.StartsWith("/"))
                                    {
                                        CleanedIntput = CleanedIntput.Remove(0, 1);
                                    }


                                    var NewCleanedInput = input.Path;
                                    if (NewCleanedInput.StartsWith("/"))
                                    {
                                        NewCleanedInput = NewCleanedInput.Remove(0, 1);
                                    }


                                    Log.Information(
                                        $"getting copy for {CleanedIntput} for SubmissionBucket {aSubmission.Project.SubmissionBucket} to {NewCleanedInput}");

                                    // Use project-scoped Submission S3 creds (from TRE Vault), not shared root creds.
                                    var minioSubHelper = await _projectS3SubHelperFactory.GetProjectS3HelperAsync(aSubmission.Project.Id);
                                    var source =
                                        await minioSubHelper.GetCopyObject(aSubmission.Project.SubmissionBucket,
                                            CleanedIntput);
                                    try
                                    {
                                        if (Files?.S3Objects != null && Files.S3Objects.Any(x => x.ETag == source.ETag))
                                        {
                                            continue;
                                        }

                                        var resultcopy =
                                            await _minioTreHelper.CopyObjectToDestination(InputBucket, NewCleanedInput,
                                                source);
                                    }
                                    catch (Exception ex)
                                    {
                                        Log.Error(ex.ToString());
                                        throw ex;
                                    }
                                }


                                if (tesMessage.Executors == null)
                                {
                                    tesMessage.Executors = new List<TesExecutor>();
                                }

                                Log.Information("looking for _AgentSettings.ImageNameToAddToToken > " +
                                                _AgentSettings.ImageNameToAddToToken);
                                var unresolvedImageCodes = new List<string>();

                                foreach (var Executor in tesMessage.Executors)
                                {
                                    if (Executor.Env == null)
                                    {
                                        Executor.Env = new Dictionary<string, string>();
                                    }

                                    // Resolve {{@imageCode}} first: everything below reads the image
                                    // field, and it needs to see the real container image.
                                    var imageResolution = await _imageCodeResolver.ResolveExecutorImageAsync(
                                        Executor.Image, aSubmission.Project.Name,
                                        aSubmission.SubmittedBy.Id.ToString());

                                    Executor.Image = imageResolution.Image;
                                    var executorImageCodes = imageResolution.ImageCodes;

                                    foreach (var unresolved in imageResolution.Unresolved)
                                    {
                                        if (!unresolvedImageCodes.Contains(unresolved, StringComparer.Ordinal))
                                        {
                                            unresolvedImageCodes.Add(unresolved);
                                        }
                                    }

                                    Executor.Env["SCHEMA"] = aSubmission.Project.Name;
                                    Executor.Env["CATALOG"] = _AgentSettings.CATALOG;

                                    Log.Information("Executor.Image > " + Executor.Image);
                                    if (await _features.IsEnabledAsync(FeatureFlags.SqlAndNotGraphQl))
                                    {
                                        if (Executor.Image.Contains(_AgentSettings.ImageNameToAddToToken))
                                        {
                                            Executor.Env["TRINO_SERVER_URL"] = _AgentSettings.URLTrinoToAdd;
                                            Executor.Env["ACCESS_TOKEN"] = Token;
                                            Executor.Env["USER_NAME"] = aSubmission.SubmittedBy.Name;


                                            if (string.IsNullOrEmpty(Executor.Env["TRINO_SERVER_URL"]))
                                            {
                                                Executor.Env["TRINO_SERVER_URL"] = "";
                                            }
                                        }

                                        if (await _features.IsEnabledAsync(FeatureFlags.EphemeralCredentials))
                                        {
                                            Log.Information(
                                                $"Injecteing credentials into environment variables for {aSubmission.Id} nub > {credentials.Count}");

                                            if (credentials != null && credentials.Any)
                                            {
                                                // Shared variables go to every executor; ones scoped to an
                                                // image code only to the executors running that image.
                                                foreach (var credential in credentials.ForImageCodes(executorImageCodes))
                                                {
                                                    Log.Information("Injected credentials with Key " + credential.Key);

                                                    Executor.Env[credential.Key] = credential.Value;
                                                }

                                                Log.Information(
                                                    $"Injected credentials into environment variables for {aSubmission.Id}");
                                            }

                                            // S3/RustFS ephemeral credentials are also exposed under the
                                            // conventional AWS/MinIO env var names so a standard S3 client in
                                            // the workload container picks them up without bespoke wiring. The
                                            // generic loop above still injects the raw accessKey/secretKey/
                                            // endPoint/bucket keys for tools that read those directly.
                                            var s3Creds = credentials?.OfType("s3");
                                            if (s3Creds != null && s3Creds.Count > 0)
                                            {
                                                string S3Val(string k) =>
                                                    s3Creds.TryGetValue(k, out var v) ? v ?? string.Empty
                                                                                      : string.Empty;

                                                var s3AccessKey = S3Val("accessKey");
                                                var s3SecretKey = S3Val("secretKey");
                                                var s3Endpoint = S3Val("endPoint");
                                                var s3SubmissionBucket = S3Val("submissionBucket");
                                                var s3OutputBucket = S3Val("outputBucket");

                                                if (!string.IsNullOrEmpty(s3AccessKey))
                                                {
                                                    Executor.Env["AWS_ACCESS_KEY_ID"] = s3AccessKey;
                                                    Executor.Env["MINIO_ACCESS_KEY"] = s3AccessKey;
                                                }

                                                if (!string.IsNullOrEmpty(s3SecretKey))
                                                {
                                                    Executor.Env["AWS_SECRET_ACCESS_KEY"] = s3SecretKey;
                                                    Executor.Env["MINIO_SECRET_KEY"] = s3SecretKey;
                                                }

                                                if (!string.IsNullOrEmpty(s3Endpoint))
                                                {
                                                    Executor.Env["AWS_ENDPOINT_URL"] = s3Endpoint;
                                                    Executor.Env["AWS_S3_ENDPOINT"] = s3Endpoint;
                                                    Executor.Env["MINIO_ENDPOINT"] = s3Endpoint;
                                                }

                                                // Region comes from config (MinioTRESettings); standard
                                                // S3 SDKs require one. Fall back to the RustFS/MinIO default.
                                                var s3Region = _config["MinioTRESettings:AWSRegion"];
                                                if (string.IsNullOrEmpty(s3Region)) s3Region = "us-east-1";
                                                Executor.Env["AWS_REGION"] = s3Region;
                                                Executor.Env["AWS_DEFAULT_REGION"] = s3Region;

                                                if (!string.IsNullOrEmpty(s3SubmissionBucket))
                                                    Executor.Env["SUBMISSION_BUCKET"] = s3SubmissionBucket;

                                                if (!string.IsNullOrEmpty(s3OutputBucket))
                                                    Executor.Env["OUTPUT_BUCKET"] = s3OutputBucket;

                                                Log.Information(
                                                    "Injected standard S3 env vars for submission {SubId}",
                                                    aSubmission.Id);
                                            }
                                        }

                                        else
                                        {
                                            Log.Information("Ephemeral Credentials not enabled for the submission");
                                        }
                                    }
                                    else
                                    {
                                        if (Executor.Image.Contains(_AgentSettings.ImageNameToAddToTokenGraphQL))
                                        {
                                            Executor.Command.Add("--Token_" + Token);
                                            Executor.Command.Add("--URL_" + _AgentSettings.URLHasuraToAdd);
                                        }
                                    }
                                }

                                _dbContext.TokensToExpire.Add(new TokenToExpire()
                                {
                                    SubId = aSubmission.Id,
                                    Token = Token
                                });
                                _dbContext.SaveChanges();


                                if (unresolvedImageCodes.Count > 0)
                                {
                                    // One unresolvable image code fails the whole job rather than
                                    // dispatching a message that still carries a literal placeholder
                                    // in an image field, which would fail obscurely at pull time.
                                    var reason =
                                        $"Could not resolve image code(s): {string.Join(", ", unresolvedImageCodes)}";

                                    Log.Error("{Function} {Reason} for sub {SubId}", "Execute", reason,
                                        aSubmission.Id);

                                    _subHelper.UpdateStatusForTre(aSubmission.Id.ToString(), StatusType.Failed,
                                        reason);

                                    processedOK = false;
                                }
                                else if (tesMessage is not null)
                                {
                                    var stringdata = JsonConvert.SerializeObject(tesMessage);

                                    // Only the shape of the message, never its contents: by this point
                                    // the executor env holds ephemeral credentials and any secrets
                                    // resolved from vault, and these logs ship to Seq.
                                    Log.Information("{Function} sending to CreateTESK {TesMessage}",
                                        "Execute", DescribeTesMessageForLog(tesMessage));

                                    CreateTesk(stringdata, aSubmission.Id, aSubmission.Project.Id,
                                        aSubmission.SubmittedBy.Id, aSubmission.TesId, OutputBucket,
                                        aSubmission.TesName);
                                }
                            }

                            // **************  TELL SUBMISSION LAYER WE DONE
                            if (processedOK)
                            {
                                try
                                {
                                    var result = _subHelper.UpdateStatusForTre(aSubmission.Id.ToString(),
                                        StatusType.TransferredToPod, "");
                                }
                                catch (Exception e)
                                {
                                    Log.Error(e,
                                        "{Function} Error sending record outcome to submission layer for sub {SubId}",
                                        "Execute", aSubmission.Id);
                                    processedOK = false;
                                }
                            }
                        }
                    }

                    catch (Exception ex)
                    {
                        Log.Error(ex, "{Function} {TreName} error occurred processing submission {SubId}", "Execute",
                            treName, aSubmission.Id);
                    }
                }
            }
        }

        public void ClearJob(string jobname)
        {
            Log.Information("{Function} Hangfire clear job: {Jobname}", "ClearJob", jobname);

            try
            {
                RecurringJob.RemoveIfExists(jobname);
            }
            catch (Exception ex)
            {
                Log.Error(ex.ToString());
            }
        }

        // Starts the Start_Credentials Camunda process for the given submission.
        // InputCollections mirrors the top-level variables as a list because the BPMN multi-instance
        // subprocess iterates over InputCollections to fan out credential creation per item.
        private async Task TriggerStartCredentialsAsync(int submissionId, string projectName, int userId,
            string? submissionBucket = null, string? outputBucket = null, string? endPoint = null)
        {
            var variables = new Dictionary<string, object>
            {
                ["project"] = projectName,
                ["user"] = userId.ToString(),
                ["submissionId"] = submissionId.ToString(),
                ["InputCollections"] = new List<Dictionary<string, object>>
                {
                    new Dictionary<string, object>
                    {
                        ["project"] = projectName,
                        ["user"] = userId.ToString(),
                        ["submissionId"] = submissionId.ToString(),
                        ["submissionBucket"] = submissionBucket ?? string.Empty,
                        ["outputBucket"] = outputBucket ?? string.Empty,
                        ["endPoint"] = endPoint ?? string.Empty
                    }
                }
            };

            await _zeebeClient.CreateProcessInstanceAsync("Start_Credentials", variables);
            Log.Information("Camunda StartCredentials triggered successfully for submission {SubmissionId}", submissionId);
        }


        private async Task<SubmissionCredentials> WaitForAndFetchCredentialsAsync(
            int submissionId, TimeSpan? timeout = null)
        {
            var maxWaitTime = timeout ?? TimeSpan.FromMinutes(5);
            var pollInterval = TimeSpan.FromSeconds(5); //Reduced polling interval for faster fetch
            var fetchedCredentials = new SubmissionCredentials();
            var fetchedPaths = new HashSet<string>(StringComparer.Ordinal);
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            Log.Information($"Starting to wait for credentials for submission {submissionId}.");

            while (stopwatch.Elapsed < maxWaitTime)
            {
                try
                {
                    var credentialRecord = await _credsDbContext.EphemeralCredentials
                        .Where(c => c.SubmissionId == submissionId && !c.IsProcessed)
                        .OrderByDescending(c => c.CreatedAt)
                        .ToListAsync();

                    foreach (var record in credentialRecord)
                    {
                        // Keyed on the vault path, not the credential type. A credential type
                        // can own several paths - the tre handler writes one per image code -
                        // and keying on the type would read the first and silently drop the rest.
                        if (string.IsNullOrEmpty(record.VaultPath) || !fetchedPaths.Add(record.VaultPath))
                        {
                            continue;
                        }

                        Log.Information(
                            $"Found {record.CredentialType} credentials for submission {submissionId} at vault path: {record.VaultPath}");

                        var credentials = await _vaultService.GetCredentialAsync(record.VaultPath);
                        if (credentials != null && credentials.Count > 0)
                        {
                            AddToSubmissionCredentials(fetchedCredentials, record.CredentialType,
                                record.VaultPath, credentials);
                            record.IsProcessed = true;

                            Log.Information(
                                $"Successfully fetched {record.CredentialType} credentials for submission {submissionId}");
                        }
                        else
                        {
                            // Nothing read, so allow a later poll to try this path again.
                            fetchedPaths.Remove(record.VaultPath);
                        }
                    }

                    if (credentialRecord.Any(r => r.IsProcessed))
                    {
                        await _credsDbContext.SaveChangesAsync();
                    }

                    // Only done once every path this pass saw has actually been read. Returning
                    // on the first success would leave a path that failed to read unretried, and
                    // a missing image-scoped path means one executor silently runs without its
                    // variables. By this point the credentials process has completed, so every
                    // row already exists and a short wait is better than a quiet gap.
                    var unread = credentialRecord.Count(r => !string.IsNullOrEmpty(r.VaultPath) && !r.IsProcessed);

                    if (fetchedCredentials.Any && unread == 0)
                    {
                        Log.Information($"Successfully fetched all credentials for submission {submissionId}");
                        return fetchedCredentials;
                    }

                    if (unread > 0)
                    {
                        Log.Warning(
                            "{Unread} vault path(s) for submission {SubmissionId} could not be read yet; retrying",
                            unread, submissionId);
                    }

                    await Task.Delay(pollInterval);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, $"Error while waiting for credentials for submission {submissionId}: {ex.Message}");
                    await Task.Delay(pollInterval);
                }
            }

            var errorMsg = $"Timeout waiting for credentials for submission {submissionId}";
            Log.Error(errorMsg);
            throw new TimeoutException(errorMsg);
        }

        /// <summary>
        /// A description of a TES message safe to log: its shape, its images, and the names
        /// of the environment variables, but none of their values.
        ///
        /// The executor env holds ephemeral credentials and secrets resolved from vault by
        /// the time the message is dispatched, and these logs are shipped to Seq, so the
        /// message itself must never be serialised into them.
        /// </summary>
        private static string DescribeTesMessageForLog(TesTask tesMessage)
        {
            var executors = tesMessage.Executors ?? new List<TesExecutor>();

            var described = executors.Select((executor, index) =>
            {
                var names = executor.Env is null || executor.Env.Count == 0
                    ? "none"
                    : string.Join(", ", executor.Env.Keys.OrderBy(name => name, StringComparer.Ordinal));

                return $"[{index}] image={executor.Image} env={{{names}}}";
            });

            return $"id={tesMessage.Id} name={tesMessage.Name} executors={executors.Count} "
                   + string.Join(" ", described);
        }

        /// <summary>
        /// Files one vault path's variables as shared or as belonging to an image code.
        ///
        /// Every handler builds its path as {tag}/{user}/{submissionId}/{project}, and the
        /// tre handler appends the image code when a variable is scoped to one. So a fifth
        /// segment means image-scoped, and is the code itself.
        /// </summary>
        private static void AddToSubmissionCredentials(SubmissionCredentials destination,
            string? credentialType, string vaultPath, Dictionary<string, object> values)
        {
            var segments = vaultPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            var imageCode = segments.Length > 4 ? segments[^1] : null;

            // Fall back to the first path segment, which is where the credential type comes
            // from when the handler builds the path.
            var type = string.IsNullOrWhiteSpace(credentialType)
                ? (segments.Length > 0 ? segments[0] : "unknown")
                : credentialType;

            var target = SelectGroup(destination, type, imageCode);

            foreach (var value in values)
            {
                target[value.Key] = value.Value?.ToString() ?? string.Empty;
            }
        }

        private static Dictionary<string, string> SelectGroup(SubmissionCredentials destination,
            string credentialType, string? imageCode)
        {
            var groups = string.IsNullOrEmpty(imageCode)
                ? destination.SharedByType
                : destination.ByImageCode;

            var key = string.IsNullOrEmpty(imageCode) ? credentialType : imageCode;

            if (!groups.TryGetValue(key, out var group))
            {
                group = new Dictionary<string, string>(StringComparer.Ordinal);
                groups[key] = group;
            }

            return group;
        }

        // Starts the Credentials_Revoke Camunda process to revoke credentials for the given submission.
        // timer controls how long (seconds) the revoke process waits before expiring credentials.
        private async Task TriggerRevokeCredentialsAsync(int submissionId, string projectName, int user, int timer)
        {
            var variables = new Dictionary<string, object>
            {
                ["submissionId"] = submissionId.ToString(),
                ["project"] = projectName,
                ["user"] = user.ToString(),
                ["timer"] = timer,
                ["InputCollections"] = new List<Dictionary<string, object>>
                {
                    new Dictionary<string, object>
                    {
                        ["submissionId"] = submissionId.ToString(),
                        ["project"] = projectName,
                        ["user"] = user.ToString(),
                        ["timer"] = timer
                    }
                }
            };

            await _zeebeClient.CreateProcessInstanceAsync("Credentials_Revoke", variables);
            Log.Information("Camunda RevokeCredentials triggered successfully for submission {SubmissionId}", submissionId);
        }


        //Seperated DB check to a private method
        private Task<List<EphemeralCredential>> GetLatestCreds(int submissionId)
        {
            return _credsDbContext.EphemeralCredentials
                .Where(e => e.SubmissionId == submissionId && e.IsProcessed != true)
                .OrderByDescending(e => e.CreatedAt)
                .ToListAsync();
        }
    }
}
