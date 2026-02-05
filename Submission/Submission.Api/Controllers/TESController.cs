using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System.ComponentModel.DataAnnotations;
using System.Text;
using Swashbuckle.AspNetCore.Annotations;
using Serilog;
using EasyNetQ;
using FiveSafesTes.Core.Models;
using FiveSafesTes.Core.Models.Enums;
using FiveSafesTes.Core.Models.Tes;
using FiveSafesTes.Core.Rabbit;
using Microsoft.AspNetCore.Authorization;
using Submission.Api.Attributes;
using Submission.Api.ContractResolvers;
using Submission.Api.Repositories.DbContexts;
using Submission.Api.Services;
using Submission.Api.Services.Contract;

namespace Submission.Api.Controllers
{
    [Route("api/[controller]")]
    
    [ApiController]
    public class TaskServiceApiController : ControllerBase
    {


        private readonly IKeyCloakService _IKeyCloakService;
        private readonly ApplicationDbContext _DbContext;
        private readonly IBus _rabbit;
        protected readonly IHttpContextAccessor _httpContextAccessor;
        private static readonly Dictionary<TesView, JsonSerializerSettings> TesJsonSerializerSettings = new()
        {
            {
                TesView.MINIMAL,
                new JsonSerializerSettings { ContractResolver = MinimalTesTaskContractResolver.Instance }
            },
            { TesView.BASIC, new JsonSerializerSettings { ContractResolver = BasicTesTaskContractResolver.Instance } },
            { TesView.FULL, new JsonSerializerSettings { ContractResolver = FullTesTaskContractResolver.Instance } }
        };

        private readonly IKeycloakTokenApiHelper _iKeycloakTokenApiHelper;

        /// <summary>
        /// Contruct a <see cref="TaskServiceApiController"/>
        /// </summary>
        /// <param name="repository">The main <see cref="ApplicationDbContext"/> database repository</param>
        /// <param name="rabbit">The main <see cref="IBus"/> easynet q sender</param>
        public TaskServiceApiController(ApplicationDbContext repository, IBus rabbit, IHttpContextAccessor httpContextAccessor, 
            IKeycloakTokenApiHelper iKeycloakTokenApiHelper, IKeyCloakService iKeyCloakService)
        {
            _DbContext = repository;
            _rabbit = rabbit;
            _httpContextAccessor = httpContextAccessor;
            _iKeycloakTokenApiHelper = iKeycloakTokenApiHelper;
            _IKeyCloakService = iKeyCloakService;
        }
        
        /// <summary>
        /// Cancel a task
        /// </summary>
        /// <param name="id">The id of the <see cref="TesTask"/> to cancel</param>
        /// <param name="cancellationToken">A<see cref="CancellationToken"/> for controlling the lifetime of the asynchronous operation.</param>
        /// <response code="200"></response>
        [HttpPost]
        [Authorize]
        [Route("/v1/tasks/{id}:cancel")]
        [ValidateModelState]
        [SwaggerOperation("CancelTask")]
        [SwaggerResponse(statusCode: 200, type: typeof(object), description: "")]
        public virtual async Task<IActionResult> CancelTask([FromRoute] [Required] string id,
            CancellationToken cancellationToken)
        {
            try { 

            var sub = _DbContext.Submissions.FirstOrDefault(x => x.Parent == null && x.TesId == id);
            if (sub== null)
            {
                return BadRequest("Invalid TesID");
            }

            var allsubs = sub.Children;
            allsubs.Add(sub);
           
            foreach (var asub in allsubs)
            {
                var newstart = DateTime.Now.ToUniversalTime();
                
                asub.TesJson = SetTesTaskStateToCancelled(asub.TesJson, asub.Id);

                    if (asub.Parent == null)
                    {
                        UpdateSubmissionStatus.UpdateStatusNoSave(sub, StatusType.CancellingChildren, "");

                    }
                    else
                    {
                        UpdateSubmissionStatus.UpdateStatusNoSave(sub, StatusType.RequestCancellation, "");

                    }
                }

            await _DbContext.SaveChangesAsync(cancellationToken);
            


            return StatusCode(200, new TesCancelTaskResponse());
            }
            catch (Exception ex)
            {
                Log.Error(ex, "{Function} Crashed", "CancelTESTask");
                throw;
            }
        }

        private string SetTesTaskStateToCancelled(string testaskstr, int subid)
        {
            try { 
            var tesTask = JsonConvert.DeserializeObject<TesTask>(testaskstr);
            if (tesTask.State == TesState.COMPLETEEnum ||
                tesTask.State == TesState.EXECUTORERROREnum ||
                tesTask.State == TesState.SYSTEMERROREnum)
            {
                Log.Information("{Function} Task {id} for subId {SubID} cannot be canceled because it is in {State} state.", "SetTesTaskStateToCancelled", tesTask.Id, subid, tesTask.State.ToString());
            }
            else if (tesTask.State != TesState.CANCELEDEnum)
            {
                Log.Information("{Function} Canceling task {id} for subID {SubID}", "SetTesTaskStateToCancelled", tesTask.Id, subid);
                tesTask.State = TesState.CANCELEDEnum;

            }

            return JsonConvert.SerializeObject(tesTask);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "{Function} Crashed", "SetTESTaskStateToCancelled");
                throw;
            }
        }

        /// <summary>
        /// Create a new task                               
        /// </summary>
        /// <param name="tesTask">The <see cref="TesTask"/> to add to the repository</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> for controlling the lifetime of the asynchronous operation.</param>
        /// <response code="200"></response>
        [HttpPost]
        [Authorize]
        [Route("/v1/tasks")]
        [ValidateModelState]
        [SwaggerOperation("CreateTask")]
        [SwaggerResponse(statusCode: 200, type: typeof(TesCreateTaskResponse), description: "")]
        public virtual async Task<IActionResult> CreateTaskAsync([FromBody] TesTask tesTask,
            CancellationToken cancellationToken)
        {
            Log.Information($"/v1/tasks route successfully entered");

            try
            {
                var usersName = (from x in User.Claims where x.Type == "preferred_username" select x.Value).First();

                var user = _DbContext.Users.FirstOrDefault(x => x.Name.ToLower() == usersName.ToLower());

                if (user == null)
                {
                    return BadRequest(
                        "User " + usersName + " doesn't exist");
                }

                if (!string.IsNullOrWhiteSpace(tesTask.Id))
                {
                    return BadRequest(
                        "Id should not be included by the client in the request; the server is responsible for generating a unique Id.");
                }

                if (string.IsNullOrWhiteSpace(tesTask.Executors?.FirstOrDefault()?.Image))
                {
                    return BadRequest("Docker container image name is required.");
                }

                foreach (var input in tesTask.Inputs ?? Enumerable.Empty<TesInput>())
                {
                    if (!input.Path.StartsWith('/'))
                    {
                        return BadRequest("Input paths in the container must be absolute paths.");
                    }
                }

                foreach (var output in tesTask.Outputs ?? Enumerable.Empty<TesOutput>())
                {
                    if (!output.Path.StartsWith('/'))
                    {
                        return BadRequest("Output paths in the container must be absolute paths.");
                    }
                }




                if (tesTask?.Resources?.BackendParameters is not null)
                {
                    var keys = tesTask.Resources.BackendParameters.Keys.Select(k => k).ToList();

                    if (keys.Count > 1 && keys.Select(k => k?.ToLowerInvariant()).Distinct().Count() != keys.Count)
                    {
                        return BadRequest("Duplicate backend_parameters were specified");
                    }




                    keys = tesTask.Resources.BackendParameters.Keys.Select(k => k).ToList();

                    // Backends shall log system warnings if a key is passed that is unsupported.
                    var unsupportedKeys = keys.Except(Enum.GetNames(typeof(TesResources.SupportedBackendParameters)))
                        .ToList();

                    if (unsupportedKeys.Count > 0)
                    {
                        Log.Warning(
                            "{Function }Unsupported keys were passed to TesResources.backend_parameters: {Keys}",
                            "CreateTaskAsync", string.Join(",", unsupportedKeys));
                    }

                    // If backend_parameters_strict equals true, backends should fail the task if any key / values are unsupported
                    if (tesTask.Resources?.BackendParametersStrict == true
                        && unsupportedKeys.Count > 0)
                    {
                        return BadRequest(
                            $"backend_parameters_strict is set to true and unsupported backend_parameters were specified: {string.Join(",", unsupportedKeys)}");
                    }


                }



                var exec = tesTask.Executors.First();
                //TODO: Implement IsDockerThere
                if (!IsDockerThere(exec.Image))
                {
                    return BadRequest("Crate Location " + exec.Image + " doesn't exist");
                }

                //TODO: External containers need copying over and change image loc

                var project = tesTask.Tags.Where(x => x.Key.ToLower() == "project").Select(x => x.Value)
                    .FirstOrDefault();
                if (string.IsNullOrWhiteSpace(project))
                {
                    return BadRequest("Tags must contain key project.");
                }
                var trestr = tesTask.Tags.Where(x => x.Key.ToLower() == "tres").Select(x => x.Value).FirstOrDefault();
                List<string> tres = new List<string>();
                if (!string.IsNullOrWhiteSpace(trestr))
                {
                    tres = trestr.Split('|').Select(x => x.ToLower()).ToList();
                }
                var dbproj = _DbContext.Projects.FirstOrDefault(x => x.Name.ToLower() == project.ToLower());

                if (dbproj == null)
                {
                    return BadRequest("Project " + project + " doesn't exist.");
                }

                // Reject if current time is past the project's end date
                if (DateTime.UtcNow > dbproj.EndDate.ToUniversalTime())
                {
                    return BadRequest($"Project '{project}' has ended (end date: {dbproj.EndDate:yyyy-MM-dd}). Cannot create new tasks.");
                }

                if (!IsUserOnProject(dbproj, usersName))
                {
                    return BadRequest("User " + User.Identity.Name + "isn't on project " + project + ".");
                }

                if (tres.Count > 0 && !AreTresOnProject(dbproj, tres))
                {
                    return BadRequest("One or more of the tres are not authorised for this project " + project + ".");
                }

                var dbtres = new List<Tre>();
                if (tres.Count == 0)
                {
                    dbtres = dbproj.Tres;
                }
                else
                {
                    foreach (var tre in tres)
                    {
                        dbtres.Add(dbproj.Tres.First(x => x.Name.ToLower() == tre.ToLower()));
                    }
                }

                if (dbtres.Count == 0)
                {
                    return BadRequest("No valid tres for this project " + project + ".");
                }

                var Token = HttpContext.Request.Headers["Authorization"].FirstOrDefault();
                Token = Token.Replace("Bearer ", "");
                var sub = new FiveSafesTes.Core.Models.Submission()
                {
                    DockerInputLocation = tesTask.Executors.First().Image,
                    Project = dbproj,
                    StartTime = DateTime.Now.ToUniversalTime(),
                    Status = StatusType.SubmissionReceived,
                    LastStatusUpdate = DateTime.Now.ToUniversalTime(),
                    SubmittedBy = user,
                    TesName = tesTask.Name,
                    SourceCrate = tesTask.Executors.First().Image,
                    QueryToken = Token
                };


                _DbContext.Submissions.Add(sub);
                await _DbContext.SaveChangesAsync(cancellationToken);
                tesTask.Id = sub.Id.ToString();
                sub.TesId = tesTask.Id;
                var tesstring = JsonConvert.SerializeObject(tesTask);
                sub.TesJson = tesstring;
                await _DbContext.SaveChangesAsync(cancellationToken);


                try
                {

                    //Send to rabbit q to processed async
                    var exch = _rabbit.Advanced.ExchangeDeclare(ExchangeConstants.Submission, "topic");

                    _rabbit.Advanced.Publish(exch, RoutingConstants.ProcessSub, false, new Message<int>(sub.Id));
                    await ControllerHelpers.AddAuditLog(LogType.CreateSubmission, user, dbproj, null, sub, null, _httpContextAccessor, User, _DbContext);

                    
                    Log.Debug("{Function} Creating task with id {Id} state {State}", "CreateTaskAsync", tesTask.Id,
                        tesTask.State);

                }
                catch (Exception ex)
                {
                    UpdateSubmissionStatus.UpdateStatusNoSave(sub, StatusType.Failed, ex.Message);
                    await _DbContext.SaveChangesAsync(cancellationToken);

                   
                    throw;

                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "{Function} Crashed", "CreateTESTaskAsync");
                throw;
            }


            return StatusCode(200, new TesCreateTaskResponse { Id = tesTask.Id });

        }

        private bool AreTresOnProject(Project project, List<string> tres)
        {
            try { 
            var projends = project.Tres.Select(x => x.Name.ToLower()).ToList();
            foreach (var tre in tres)
            {
                if (! projends.Contains(tre))
                {
                    return false;
                }
            }
            return true;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "{Function} Crashed", "AreTRESonProject");
                throw;
            }
        }

        private bool IsDockerThere(string dockerloc)
        {
            //TODO: Implement this
            return true;
        }

        private bool IsUserOnProject(Project project, string username)
        {
            try 
            { 
                // To match the username's case-insensitive in KeyCloak and DB
                return project.Users.Any(x => x.Name.ToLower() == username.ToLower());
            }
            catch (Exception ex)
            {
                Log.Error(ex, "{Function} Crashed", "IsUserOnProject_TES");
                throw;
            }
        }

        /// <summary>
        /// GetServiceInfo provides information about the service, such as storage details, resource availability, and  other documentation.
        /// </summary>
        /// <response code="200"></response>
        [AllowAnonymous]
        [HttpGet]
        [Route("/v1/service-info")]
        [ValidateModelState]
        [SwaggerOperation("GetServiceInfo")]
        [SwaggerResponse(statusCode: 200, type: typeof(TesServiceInfo), description: "")]
        public virtual IActionResult GetServiceInfo()
        {
            try { 
            var serviceInfo = new TesServiceInfo
            {
                Name = "DARE FX",
                Doc = string.Empty,
                Storage = new List<string>(){ "s3://ohsu-compbio-funnel/storage" },
                TesResourcesSupportedBackendParameters =
                    Enum.GetNames(typeof(TesResources.SupportedBackendParameters)).ToList()
            };

            Log.Information(
                "{Function} Name: {Name} Doc: {Doc} Storage: {Storage} TesResourcesSupportedBackendParameters: {Params}",
                "GetServiceInfo", serviceInfo.Name, serviceInfo.Doc, serviceInfo.Storage,
                string.Join(",", serviceInfo.TesResourcesSupportedBackendParameters));
            return StatusCode(200, serviceInfo);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "{Function} Crashed", "GetServiceInfo");
                throw;
            }
        }

        /// <summary>
        /// GetServiceInfo provides information about the service, such as storage details, resource availability, and  other documentation.
        /// </summary>
        /// <response code="200"></response>
        [HttpGet]
        [AllowAnonymous]
        [Route("/v1/get_test_tes")]
        [ValidateModelState]
        [SwaggerOperation("GetTestTes")]
        [SwaggerResponse(statusCode: 200, type: typeof(string), description: "")]
        public virtual IActionResult GetTestTes()
        {
            try { 
				var test = new TesTask()
				{
				   
					Name = "Atest",
					Executors = new List<TesExecutor>()
					{
						new TesExecutor()
						{
							Image = @"\\minio\justin1.crate",
						  
						}
					},
					Tags = new Dictionary<string, string>()
					{
						{ "project", "Head" },
						{ "tres", "SAIL|DPUK" }
					}

				};


				return StatusCode(200, test);

            }
            catch (Exception ex)
            {
                Log.Error(ex, "{Function} Crashed", "GetTestTES");
                throw;
            }

        }




       

        /// <summary>
        /// Get a task. TaskView is requested as such: \&quot;v1/tasks/{id}?view&#x3D;FULL\&quot;
        /// </summary>
        /// <param name="id">The id of the <see cref="TesTask"/> to get</param>
        /// <param name="view">OPTIONAL. Affects the fields included in the returned Task messages. See TaskView below.   - MINIMAL: Task message will include ONLY the fields:   Task.Id   Task.State  - BASIC: Task message will include all fields EXCEPT:   Task.ExecutorLog.stdout   Task.ExecutorLog.stderr   Input.content   TaskLog.system_logs  - FULL: Task message includes all fields.</param>
        /// <param name="cancellationToken">A<see cref="CancellationToken"/> for controlling the lifetime of the asynchronous operation.</param>
        /// <response code="200"></response>
        [HttpGet]
        [AllowAnonymous]
        [Route("/v1/tasks/{id}")]
        [ValidateModelState]
        [SwaggerOperation("GetTask")]
        [SwaggerResponse(statusCode: 200, type: typeof(TesTask), description: "")]
        public virtual async Task<IActionResult> GetTaskAsync([FromRoute] [Required] string id, [FromQuery] string view,
            CancellationToken cancellationToken)
        {
            try { 

            var sub = _DbContext.Submissions.FirstOrDefault(x => x.ParentId == null && x.TesId == id);
            if (sub == null)
            {
                return BadRequest("Invalid ID.");
            }

            var tesobj = JsonConvert.DeserializeObject<TesTask>(sub.TesJson);
            return TesJsonResult(tesobj, view);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "{Function} Crashed", "GetTesTaskAsync");
                throw;
            }
        }

        /// <summary>
        /// List tasks. TaskView is requested as such: \&quot;v1/tasks?view&#x3D;BASIC\&quot;
        /// </summary>
        /// <param name="namePrefix">OPTIONAL. Filter the list to include tasks where the name matches this prefix. If unspecified, no task name filtering is done.</param>
        /// <param name="pageSize">OPTIONAL. Number of tasks to return in one page. Must be less than 2048. Defaults to 256.</param>
        /// <param name="pageToken">OPTIONAL. Page token is used to retrieve the next page of results. If unspecified, returns the first page of results. See ListTasksResponse.next_page_token</param>
        /// <param name="view">OPTIONAL. Affects the fields included in the returned Task messages. See TaskView below.   - MINIMAL: Task message will include ONLY the fields:   Task.Id   Task.State  - BASIC: Task message will include all fields EXCEPT:   Task.ExecutorLog.stdout   Task.ExecutorLog.stderr   Input.content   TaskLog.system_logs  - FULL: Task message includes all fields.</param>
        /// <param name="cancellationToken">A<see cref="CancellationToken"/> for controlling the lifetime of the asynchronous operation.</param>
        /// <response code="200"></response>
        [HttpGet]
        [AllowAnonymous]
        [Route("/v1/tasks")]
        [ValidateModelState]
        [SwaggerOperation("ListTasks")]
        [SwaggerResponse(statusCode: 200, type: typeof(TesListTasksResponse), description: "")]
        public virtual async Task<IActionResult> ListTasks([FromQuery] string namePrefix, [FromQuery] long? pageSize,
            [FromQuery] string pageToken, [FromQuery] string view, CancellationToken cancellationToken)
        {
            try { 
            var decodedPageToken =
                pageToken is not null ? Encoding.UTF8.GetString(Microsoft.AspNetCore.Authentication.Base64UrlTextEncoder.Decode(pageToken)) : null;

            if (pageSize < 1 || pageSize > 2047)
            {
                Log.Error("{Function} pageSize invalid {pageSize}", "ListTasks", pageSize);
                return BadRequest("If provided, pageSize must be greater than 0 and less than 2048. Defaults to 256.");
            }

            var pageSizeInt = pageSize.HasValue ? (int)pageSize : 256;

            var initsubs = _DbContext.Submissions.Where(x =>
                x.Parent == null && (string.IsNullOrWhiteSpace(namePrefix) ||
                                     x.TesName.ToLower().StartsWith(namePrefix.ToLower())));
            var count = initsubs.Count();

            var start = string.IsNullOrWhiteSpace(decodedPageToken) ? 0 : int.Parse(decodedPageToken, System.Globalization.CultureInfo.InvariantCulture);
            var continuation = (pageSizeInt > start + count) ? (start + count).ToString("G", System.Globalization.CultureInfo.InvariantCulture) : null;
            var finalList = await Task.FromResult((continuation, _DbContext.Submissions.Skip(start).Take(pageSizeInt).Where(x =>
                x.Parent == null && (string.IsNullOrWhiteSpace(namePrefix) ||
                                     x.TesName.ToLower().StartsWith(namePrefix.ToLower())))));

            var tesTasks = finalList.Item2.Where(x => !string.IsNullOrWhiteSpace(x.TesJson))
                .Select(x => JsonConvert.DeserializeObject<TesTask>(x.TesJson)).OfType<TesTask>();


            var nextPageToken = finalList.continuation;
            var encodedNextPageToken = nextPageToken is not null ? Microsoft.AspNetCore.Authentication.Base64UrlTextEncoder.Encode(Encoding.UTF8.GetBytes(nextPageToken)) : null;
            var response = new TesListTasksResponse { Tasks = tesTasks.ToList(), NextPageToken = encodedNextPageToken };

            return TesJsonResult(response, view);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "{Function} Crashed", "ListTasks");
                throw;
            }
        }

        private static FiveSafesTes.Core.Models.Submission Clone(FiveSafesTes.Core.Models.Submission obj)
        {
            try { 
            if (ReferenceEquals(obj, null)) return default;

            return JsonConvert.DeserializeObject<FiveSafesTes.Core.Models.Submission>(JsonConvert.SerializeObject(obj), new JsonSerializerSettings { ObjectCreationHandling = ObjectCreationHandling.Replace });
            }
            catch (Exception ex)
            {
                Log.Error(ex, "{Function} Crashed", "SubmissionClone");
                throw;
            }
        }

        private IActionResult TesJsonResult(object value, string view)
        {
            
            TesView viewEnum;

            try
            {
                viewEnum = string.IsNullOrEmpty(view) ? TesView.MINIMAL : Enum.Parse<TesView>(view, true);
            }
            catch
            {
                Log.Error("{Function }Invalid view parameter value. If provided, it must be one of: {Names}",
                    "TesJsonResult", string.Join(", ", Enum.GetNames(typeof(TesView))));
                return BadRequest(
                    $"Invalid view parameter value. If provided, it must be one of: {string.Join(", ", Enum.GetNames(typeof(TesView)))}");
            }

            var jsonResult = new JsonResult(value, TesJsonSerializerSettings[viewEnum]) { StatusCode = 200 };

            return jsonResult;
        }

        private enum TesView
        {
            MINIMAL,
            BASIC,
            FULL
        }
    }
}

