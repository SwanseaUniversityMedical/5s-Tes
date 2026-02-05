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
using Microsoft.FeatureManagement;
using Newtonsoft.Json;
using Serilog;

namespace Agent.Api
{
    public interface IDoAgentWork
    {
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
        private readonly IHasuraAuthenticationService _hasuraAuthenticationService;
        private readonly IDareClientWithoutTokenHelper _dareHelper;
        private readonly AgentSettings _AgentSettings;
        private readonly MinioSettings _minioSettings;
        private readonly IKeyCloakService _keyCloakService;
        private readonly TreKeyCloakSettings _TreKeyCloakSettings;
        private readonly IEncDecHelper _encDecHelper;
        private readonly IFeatureManager _features;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly CredentialsDbContext _credsDbContext;
        private readonly IVaultCredentialsService _vaultService;
        private readonly IConfiguration _config;


        public DoAgentWork(IServiceProvider serviceProvider,
            ApplicationDbContext dbContext,
            ISubmissionHelper subHelper,
            IMinioTreHelper minioTreHelper,
            IMinioSubHelper minioSubHelper,
            IHasuraAuthenticationService hasuraAuthenticationService,
            IDareClientWithoutTokenHelper dareHelper,
            AgentSettings AgentSettings,
            MinioSettings minioSettings,
            IKeyCloakService keyCloakService,
            TreKeyCloakSettings TreKeyCloakSettings,
            IEncDecHelper encDecHelper,
            IFeatureManager features,
            IHttpClientFactory httpClientFactory,
            CredentialsDbContext credsDbContext,
            IVaultCredentialsService vaultService,
            IConfiguration config
        )
        {
            _serviceProvider = serviceProvider;
            _dbContext = dbContext;
            _subHelper = subHelper;

            _minioTreHelper = minioTreHelper;
            _minioSubHelper = minioSubHelper;

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
            _TreKeyCloakSettings = TreKeyCloakSettings;
            _encDecHelper = encDecHelper;
            _features = features;
            _httpClientFactory = httpClientFactory;
            _credsDbContext = credsDbContext;
            _vaultService = vaultService;

            _config = config;
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
                Log.Information("{Function} Check TES : {TaskId},  TES : {TesId}, sub: {SubId}", "CheckTES", taskID,
                    tesId, subId);
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
                    Log.Information("{Function} Response status {State}", "CheckTES", response.StatusCode);
                    Console.WriteLine(response.StatusCode);

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
                                    Log.Information(
                                        $"  CloseSubmissionForTre with status.state subId {subId.ToString()} == EXECUTOR_ERROR or SYSTEM_ERROR ");
                                    try
                                    {
                                        result = _subHelper.CloseSubmissionForTre(subId.ToString(), StatusType.Failed,
                                            "", "");
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
                                    _subHelper.UpdateStatusForTre(subId.ToString(), StatusType.DataOutApprovalRejected,
                                        " No Files to review ");
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
                        }
                        else
                            Log.Information("{Function} No change", "CheckTES");
                    }
                    else
                    {
                        Log.Error("{Function} HTTP Request {url} failed with status code {code}", "CheckTES", url,
                            response.StatusCode);
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
            Log.Information("{Function} DoAgentWork running", "Execute");
            // control use of dependency injection
            using (var scope = _serviceProvider.CreateScope())
            {
                // OPTIONS
                var useRabbit = _AgentSettings.UseRabbit;
                var useTESK = _AgentSettings.UseTESK;

                Log.Information("{Function} useRabbit {useRabbit}", "Execute", useRabbit);
                Log.Information("{Function} useTESK {useTESK}", "Execute", useTESK);
                if (await _features.IsEnabledAsync(FeatureFlags.DemoAllInOne))
                {
                    Log.Information("{Function} Demo Mode is on, simulating execution..", "Execute");
                }

                var cancelsubprojs = _subHelper.GetRequestCancelSubsForTre();
                if (cancelsubprojs != null)
                {
                    foreach (var cancelsubproj in cancelsubprojs)
                    {
                        _subHelper.UpdateStatusForTre(cancelsubproj.Id.ToString(), StatusType.CancellationRequestSent,
                            "");
                        //TODO Do we need to call Hutch or other stuff to cancel and do other cancel stuff
                        _subHelper.CloseSubmissionForTre(cancelsubproj.Id.ToString(), StatusType.Cancelled, "", "");
                    }
                }

                // Get list of submissions
                List<Submission> listOfSubmissions;

                try
                {
                    listOfSubmissions = _subHelper.GetWaitingSubmissionForTre();
                }
                catch (Exception e)
                {
                    Log.Error(e, "{Function} Error getting submissions", "Execute");

                    throw;
                }


                Log.Information("{Function} listOfSubmissions {listOfSubmissions}", "Execute",
                    listOfSubmissions?.Count);
                foreach (var aSubmission in listOfSubmissions)
                {
                    try
                    {
                        Log.Information("{Function}Submission: {submission}", "Execute", aSubmission.Id);

                        // Check user is allowed on the project
                        if (!_subHelper.IsUserApprovedOnProject(aSubmission.Project.Id, aSubmission.SubmittedBy.Id))
                        {
                            Log.Error(
                                "{Function }User {UserID}/project {ProjectId} is not value for this submission {submission}",
                                "Execute",
                                aSubmission.SubmittedBy.Id, aSubmission.Project.Id, aSubmission);
                            // record error with submission layer
                            var result =
                                _subHelper.UpdateStatusForTre(aSubmission.Id.ToString(), StatusType.InvalidUser, "");
                            result = _subHelper.CloseSubmissionForTre(aSubmission.Id.ToString(), StatusType.Failed, "",
                                "");
                        }


                        else
                        {
                            Dictionary<string, Dictionary<string, object>> credentials =
                                new Dictionary<string, Dictionary<string, object>>();

                            if (await _features.IsEnabledAsync(FeatureFlags.EphemeralCredentials))
                            {
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
                                        await TriggerStartCredentialsAsync(aSubmission.Id, project,
                                            aSubmission.SubmittedBy.Id);
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
                                        Log.Error("Credential process errored for submission {SubId}.", aSubmission.Id);

                                        var latestRow = creds.First();
                                        latestRow.ErrorMessage = "Credential process failed";
                                        await _credsDbContext.SaveChangesAsync();

                                        _subHelper.UpdateStatusForTre(aSubmission.Id.ToString(),
                                            StatusType.RequestCancellation, "Credential process failed");
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
                            if (await _features.IsEnabledAsync(FeatureFlags.DemoAllInOne))
                            {
                                try
                                {
                                    _subHelper.SimulateSubmissionProcessing(aSubmission);
                                }
                                catch (Exception e)
                                {
                                    Log.Error(e, "{Function} Simulation failed for sub {SubId}", "Execute",
                                        aSubmission.Id);
                                    processedOK = false;
                                }
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
                                    Log.Error(e, "{Function} Send rabbit failed for sub {SubId}", "Execute",
                                        aSubmission.Id);
                                    processedOK = false;
                                }
                            }

                            // **************  SEND TO TESK
                            if (useTESK)
                            {
                                Log.Information("{Function}  SEND TO TESK ", "Execute");
                                var arr = new HttpClient();
                                var Token = "";

                                var role = aSubmission.Project.Name; //TODO Check

                                if (await _features.IsEnabledAsync(FeatureFlags.GenerateAccounts) &&
                                    await _features.IsEnabledAsync(FeatureFlags.SqlAndNotGraphQl))
                                {
                                    var Acount = _dbContext.ProjectAcount.FirstOrDefault(x =>
                                        x.Name == aSubmission.Project.Name + aSubmission.SubmittedBy.Name);

                                    var TokenIN = await _keyCloakService.GenAccessTokenSimple(Acount.Name,
                                        _encDecHelper.Decrypt(Acount.Pass), _TreKeyCloakSettings.TokenRefreshSeconds);

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

                                    var source =
                                        await _minioSubHelper.GetCopyObject(aSubmission.Project.SubmissionBucket,
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
                                foreach (var Executor in tesMessage.Executors)
                                {
                                    if (Executor.Env == null)
                                    {
                                        Executor.Env = new Dictionary<string, string>();
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

                                            if (credentials != null && credentials.Count > 0)
                                            {
                                                foreach (var outerKey in credentials)
                                                {
                                                    if (outerKey.Value is IDictionary<string, object>
                                                        innerDict) //The format is dictionary within a dictionary
                                                    {
                                                        foreach (var inner in innerDict)
                                                        {
                                                            var key = inner.Key;
                                                            var value = inner.Value?.ToString() ?? string.Empty;
                                                            Log.Information("Injected credentials with Key " + key);

                                                            Executor.Env[key] = value;
                                                        }
                                                    }
                                                }

                                                Log.Information(
                                                    $"Injected credentials into environment variables for {aSubmission.Id}");
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


                                if (tesMessage is not null)
                                {
                                    var stringdata = JsonConvert.SerializeObject(tesMessage);
                                    Log.Information("{Function} tesMessage is not null runhing CreateTESK {tesMessage}",
                                        "Execute", stringdata);

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
                        Log.Error(ex, "{Function } Error occured processing submission {SubId}", "Execute",
                            aSubmission.Id);
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

        private async Task TriggerStartCredentialsAsync(int submissionId, string projectName, int userId)
        {
            var payload = new
            {
                records = new[]
                {
                    new
                    {
                        project = projectName,
                        user = userId.ToString(),
                        submissionId = submissionId.ToString()
                    }
                }
            };

            var jsonPayload = Newtonsoft.Json.JsonConvert.SerializeObject(payload);
            var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            var camundaWebhookUrl = _config["CredentialAPISettings:StartWebhookUrl"];

            using var httpClient = _httpClientFactory.CreateClient();
            httpClient.Timeout = TimeSpan.FromMinutes(2);

            var response = await httpClient.PostAsync(camundaWebhookUrl, content);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                Log.Error("Camunda webhook call failed for submission {SubmissionId}. Error: {Error}", submissionId,
                    error);
                throw new Exception($"Camunda webhook call failed: {response.StatusCode}");
            }

            Log.Information("Camunda StartCredentials triggered successfully for submission {SubmissionId}",
                submissionId);
        }


        private async Task<Dictionary<string, Dictionary<string, object>>> WaitForAndFetchCredentialsAsync(
            int submissionId, TimeSpan? timeout = null)
        {
            var maxWaitTime = timeout ?? TimeSpan.FromMinutes(5);
            var pollInterval = TimeSpan.FromSeconds(5); //Reduced polling interval for faster fetch
            var fetchedCredentials = new Dictionary<string, Dictionary<string, object>>();
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
                        if (!fetchedCredentials.ContainsKey(record.CredentialType) &&
                            !string.IsNullOrEmpty(record.VaultPath))
                        {
                            Log.Information(
                                $"Found {record.CredentialType} credentials for submission {submissionId} at vault path: {record.VaultPath}");

                            var credentials = await _vaultService.GetCredentialAsync(record.VaultPath);
                            if (credentials != null && credentials.Count > 0)
                            {
                                fetchedCredentials[record.CredentialType] = credentials;
                                record.IsProcessed = true;

                                Log.Information(
                                    $"Successfully fetched {record.CredentialType} credentials for submission {submissionId}");
                            }
                        }
                    }

                    if (credentialRecord.Any(r => r.IsProcessed))
                    {
                        await _credsDbContext.SaveChangesAsync();
                    }

                    if (fetchedCredentials.Count > 0)
                    {
                        Log.Information($"Successfully fetched all credentials for submission {submissionId}");
                        return fetchedCredentials;
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

        private async Task TriggerRevokeCredentialsAsync(int submissionId, string projectName, int user, int timer)
        {
            var payload = new
            {
                records = new[]
                {
                    new
                    {
                        submissionId = submissionId.ToString(),
                        project = projectName,
                        user = user.ToString(),
                        timer = timer
                    }
                }
            };

            var jsonPayload = Newtonsoft.Json.JsonConvert.SerializeObject(payload);
            var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            // use the correct config key (matches appsettings: RevokeWebhookUrl)
            var camundaWebhookUrl = _config["CredentialAPISettings:RevokeWebhookUrl"];

            if (string.IsNullOrWhiteSpace(camundaWebhookUrl))
            {
                throw new InvalidOperationException(
                    "Configuration 'CredentialAPISettings:RevokeWebhookUrl' is missing or empty.");
            }

            if (!Uri.TryCreate(camundaWebhookUrl, UriKind.Absolute, out var webhookUri))
            {
                throw new InvalidOperationException($"Invalid webhook URL in configuration: {camundaWebhookUrl}");
            }

            using var httpClient = _httpClientFactory.CreateClient();
            httpClient.Timeout = TimeSpan.FromMinutes(2);

            var response = await httpClient.PostAsync(webhookUri, content);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                Log.Error("Camunda revoke webhook failed for submission {SubmissionId}. Error: {Error}", submissionId,
                    error);
                throw new Exception($"Camunda revoke webhook call failed: {response.StatusCode}");
            }

            Log.Information("Camunda RevokeCredentials triggered successfully for submission {SubmissionId}",
                submissionId);
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
