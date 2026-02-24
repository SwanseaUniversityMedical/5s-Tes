using FiveSafesTes.Core.Models;
using FiveSafesTes.Core.Models.Tes;
using FiveSafesTes.Core.Models.ViewModels;
using FiveSafesTes.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using Newtonsoft.Json;
using Serilog;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Submission.Web.Models;
using Submission.Web.Services;

namespace Submission.Web.Controllers
{
    public class SubmissionController : Controller
    {
        private readonly IDareClientHelper _clientHelper;
        private readonly IConfiguration _configuration;
        private readonly URLSettingsFrontEnd _URLSettingsFrontEnd;
        private readonly IKeyCloakService _IKeyCloakService;

        public SubmissionController(IDareClientHelper client, IConfiguration configuration,
            URLSettingsFrontEnd URLSettingsFrontEnd, IKeyCloakService IKeyCloakService)
        {
            _clientHelper = client;
            _configuration = configuration;
            _URLSettingsFrontEnd = URLSettingsFrontEnd;
            _IKeyCloakService = IKeyCloakService;
        }

        public IActionResult Instructions()
        {
            var url = _configuration["DareAPISettings:HelpAddress"];
            return View(model: url);
        }

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> SubmissionWizard(SubmissionWizard model)
        {
            if (!ModelState.IsValid) // SonarQube security
            {
                var errors = ModelState
                    .Where(x => x.Value.Errors.Count > 0)
                    .Select(x => new { Field = x.Key, Errors = x.Value.Errors.Select(e => e.ErrorMessage) })
                    .ToList();

                return BadRequest($"Model validation failed: {string.Join(", ", errors.SelectMany(e => e.Errors))}");
            }

            try
            {
                var listOfTre = "";
                var imageUrl = "";
                var paramlist = new Dictionary<string, string>();
                paramlist.Add("projectId", model.ProjectId.ToString());
                var project = await _clientHelper.CallAPIWithoutModel<Project?>(
                    "/api/Project/GetProject/", paramlist);

                if (model.TreRadios == null)
                {
                    var paramList = new Dictionary<string, string>();
                    paramList.Add("projectId", model.ProjectId.ToString());
                    var tre = await _clientHelper.CallAPIWithoutModel<List<Tre>>("/api/Project/GetTresInProject/",
                        paramList);
                    List<string> namesList = tre.Select(test => test.Name).ToList();
                    listOfTre = string.Join("|", namesList);
                }
                else
                {
                    listOfTre = string.Join("|",
                        model.TreRadios.Where(info => info.IsSelected).Select(info => info.Name));
                }

                if (model.OriginOption == CrateOrigin.External)
                {
                    imageUrl = model.ExternalURL;
                }
                else
                {
                    var paramss = new Dictionary<string, string>();
                    paramss.Add("bucketName", project.SubmissionBucket);
                    if (model.File != null)
                    {
                        await _clientHelper.CallAPIToSendFile<APIReturn>("/api/Project/UploadToMinio", "file",
                            model.File, paramss);
                    }

                    var minioEndpoint =
                        await _clientHelper.CallAPIWithoutModel<MinioEndpoint>("/api/Project/GetMinioEndPoint");
                    // Don't add http:// — minioEndpoint.Url already has it.
                    imageUrl = minioEndpoint.Url + "/browser/" + project.SubmissionBucket + "/" + model.File.FileName;
                }

                var tesTask = new TesTask()
                {
                    Name = model.TESName,
                    Executors = new List<TesExecutor>()
                    {
                        new TesExecutor()
                        {
                            Image = imageUrl,
                        }
                    },
                    Tags = new Dictionary<string, string>()
                    {
                        { "project", project.Name },
                        { "tres", listOfTre },
                        { "author", HttpContext.User.FindFirst("name").Value }
                    }
                };

                var result = await _clientHelper.CallAPI<TesTask, TesTask?>("/v1/tasks", tesTask);
                return RedirectToAction("GetASubmission", new { id = result.Id });
            }
            catch (Exception e)
            {
                Log.Error(e, "Exception in {Function}");
                return BadRequest(e.Message);
            }
        }

        public static string GetContentType(string fileName)
        {
            // Create a new FileExtensionContentTypeProvider
            var provider = new FileExtensionContentTypeProvider();

            // Try to get the content type based on the file name's extension
            if (provider.TryGetContentType(fileName, out var contentType))
            {
                return contentType;
            }

            // If the content type cannot be determined, provide a default value
            return "application/octet-stream"; // This is a common default for unknown file types
        }

        [HttpGet]
        public IActionResult GetAllSubmissions()
        {
            var minio = _clientHelper.CallAPIWithoutModel<MinioEndpoint>("/api/Project/GetMinioEndPoint").Result;
            ViewBag.minioendpoint = minio?.Url;
            ViewBag.URLBucket = _URLSettingsFrontEnd.MinioUrl;

            var res = _clientHelper.CallAPIWithoutModel<List<FiveSafesTes.Core.Models.Submission>>("/api/Submission/GetAllSubmissions/").Result
                .Where(x => x.Parent == null).ToList();

            return View(res);
        }

        [HttpGet]
        public IActionResult GetASubmission(int id)
        {
            if (!ModelState.IsValid) // SonarQube security
            {
                return BadRequest("Invalid model state");
            }

            var res = _clientHelper.CallAPIWithoutModel<FiveSafesTes.Core.Models.Submission>($"/api/Submission/GetASubmission/{id}").Result;

            var minio = _clientHelper.CallAPIWithoutModel<MinioEndpoint>("/api/Project/GetMinioEndPoint").Result;
            ViewBag.minioendpoint = minio?.Url;
            ViewBag.URLBucket = _URLSettingsFrontEnd.MinioUrl;

            var model = new SubmissionInfo()
            {
                Submission = res,
                Stages = _clientHelper.CallAPIWithoutModel<Stages>("/api/Submission/StageTypes/").Result
            };

            return View(model);
        }

        [HttpPost]
        [Authorize]
        public async Task<ActionResult> SubmitDemoTes(AddiSubmissionWizard model, string Executors, string SQL)
        {
            if (!ModelState.IsValid) // SonarQube security
            {
                return View("/");
            }

            try
            {
                var tres = "";
                var paramlist = new Dictionary<string, string>();
                paramlist.Add("projectId", model.ProjectId.ToString());
                var project = await _clientHelper.CallAPIWithoutModel<Project?>(
                    "/api/Project/GetProject/", paramlist) ?? throw new NullReferenceException("Project not found");

                var treSelection = model.TreRadios.Where(x => x.IsSelected).ToList();
                if (treSelection.Count == 0)
                {
                    var paramList = new Dictionary<string, string>();
                    paramList.Add("projectId", model.ProjectId.ToString());
                    var tre = await _clientHelper.CallAPIWithoutModel<List<Tre>>("/api/Project/GetTresInProject/",
                        paramList);
                    List<string> namesList = tre.Select(test => test.Name).ToList();
                    tres = string.Join("|", namesList);
                }
                else
                {
                    tres = string.Join("|",
                        model.TreRadios.Where(info => info.IsSelected).Select(info => info.Name));
                }

                var tesTask = new TesTask();
                if (!string.IsNullOrWhiteSpace(model.JsonData))
                {
                    tesTask = JsonConvert.DeserializeObject<TesTask>(model.JsonData) ??
                              throw new NullReferenceException("Json data not returned");

                    if (tesTask.Tags == null || tesTask.Tags.Count == 0)
                    {
                        tesTask.Tags = new Dictionary<string, string>()
                        {
                            { "project", project.Name },
                            { "tres", tres },
                            { "author", HttpContext.User.FindFirst("name").Value }
                        };
                    }
                }

                await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                await _clientHelper.CallAPI<TesTask, TesTask?>("/v1/tasks", tesTask);

                return Ok();
            }
            catch (Exception e)
            {
                Log.Error(e, "Exception in {Function}");
                return BadRequest(e.Message);
            }
        }

        [HttpPost]
        [Authorize]
        public async Task<ActionResult> AddiSubmissionWizard(AddiSubmissionWizard model, string Executors, string SQL)
        {
            if (!ModelState.IsValid) // SonarQube security
            {
                return View("/");
            }

            try
            {
                var listOfTre = "";

                var paramlist = new Dictionary<string, string>();
                paramlist.Add("projectId", model.ProjectId.ToString());
                var project = await _clientHelper.CallAPIWithoutModel<Project?>(
                    "/api/Project/GetProject/", paramlist);

                var test = new TesTask();
                var tesExecutors = new List<TesExecutor>();

                if (string.IsNullOrEmpty(Executors) == false && Executors != "null")
                {
                    bool First = true;
                    List<Executors> executorsList = JsonConvert.DeserializeObject<List<Executors>>(Executors);
                    foreach (var ex in executorsList)
                    {
                        if (string.IsNullOrEmpty(ex.Image)) continue;

                        Dictionary<string, string> EnvVars = new Dictionary<string, string>();
                        foreach (var anENV in ex.ENV)
                        {
                            var keyval = anENV.Split('=', 2);
                            EnvVars[keyval[0]] = keyval[1];
                        }

                        var exet = new TesExecutor()
                        {
                            Image = ex.Image,
                            Command = ex.Command,
                            Env = EnvVars
                        };
                        tesExecutors.Add(exet);
                    }
                }

                var TreDataTreData = model.TreRadios.Where(x => x.IsSelected == true).ToList();

                if (TreDataTreData.Count == 0)
                {
                    var paramList = new Dictionary<string, string>();
                    paramList.Add("projectId", model.ProjectId.ToString());
                    var tre = await _clientHelper.CallAPIWithoutModel<List<Tre>>("/api/Project/GetTresInProject/",
                        paramList);
                    List<string> namesList = tre.Select(test => test.Name).ToList();
                    listOfTre = string.Join("|", namesList);
                }
                else
                {
                    listOfTre = string.Join("|",
                        model.TreRadios.Where(info => info.IsSelected).Select(info => info.Name));
                }

                test = new TesTask();

                if (string.IsNullOrEmpty(model.RawInput) == false)
                {
                    test = JsonConvert.DeserializeObject<TesTask>(model.RawInput);
                }


                if (string.IsNullOrEmpty(model.TESName) == false)
                {
                    test.Name = model.TESName;
                }

                if (string.IsNullOrEmpty(model.TESDescription) == false)
                {
                    test.Description = model.TESDescription;
                }

                if (tesExecutors.Count > 0)
                {
                    if (test.Executors == null || test.Executors.Count == 0)
                    {
                        test.Executors = tesExecutors;
                    }
                    else
                    {
                        test.Executors.AddRange(tesExecutors);
                    }
                }

                if (string.IsNullOrEmpty(model.Query) == false)
                {
                    var QueryExecutor = new TesExecutor()
                    {
                        Image = _URLSettingsFrontEnd.QueryImageGraphQL,
                        Command = new List<string>
                        {
                            "/usr/bin/dotnet",
                            "/app/Tre-Hasura.dll",
                            "--Query_" + model.Query
                        }
                    };


                    if (SQL == "true")
                    {
                        QueryExecutor.Image = _URLSettingsFrontEnd.QueryImageSQL;
                        QueryExecutor.Command = new List<string>()
                        {
                            "/bin/bash",
                            "/workspace/entrypoint.sh",
                            $"--Query={model.Query}"
                        };
                        QueryExecutor.Env = new Dictionary<string, string>()
                        {
                            ["LOCATION"] = "/workspace/data/results.csv",
                        };
                    }


                    if (test.Executors == null)
                    {
                        test.Executors = new List<TesExecutor>();
                        test.Executors.Add(QueryExecutor);
                    }
                    else
                    {
                        test.Executors.Insert(0, QueryExecutor);
                    }
                }

                if (test.Outputs == null || test.Outputs.Count == 0)
                {
                    test.Outputs = new List<TesOutput>()
                    {
                        new TesOutput()
                        {
                            Url = "",
                            Name = "aName",
                            Description = "ADescription",
                            Path = "/app/data",
                            Type = TesFileType.DIRECTORYEnum,
                        }
                    };
                    if (SQL == "true")
                    {
                        test.Outputs[0].Path = "/workspace/data";
                    }
                }

                if (test.Tags == null || test.Tags.Count == 0)
                {
                    test.Tags = new Dictionary<string, string>()
                    {
                        { "project", project.Name },
                        { "tres", listOfTre },
                        { "author", HttpContext.User.FindFirst("name").Value }
                    };
                }

                if (string.IsNullOrEmpty(model.DataInputPath) == false)
                {
                    if (test.Inputs == null)
                    {
                        test.Inputs = new List<TesInput>();
                    }
                    test.Inputs.Add(new TesInput()
                    {
                        Path = model.DataInputPath,
                        Type = Enum.Parse<TesFileType>(model.DataInputType),
                        Name = "",
                        Description = "",
                        Url = "a",
                        Content = ""
                    });
                }

                var context = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                var Token = await _IKeyCloakService.RefreshUserToken(context);

                var result = await _clientHelper.CallAPI<TesTask, TesTask?>("/v1/tasks", test);


                return Ok();
            }
            catch (Exception e)
            {
                Log.Error(e, "Exception in {Function}");
                return BadRequest(e.Message);
            }
        }

        [HttpPost]
        [Authorize]
        public async Task<ActionResult> SubmissionSqlWizardAction(
            SubmissionWizardV2 model, string? CustomExecutors, string Mode)
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState
                    .Where(x => x.Value.Errors.Count > 0)
                    .SelectMany(x => x.Value.Errors.Select(e => e.ErrorMessage));
                return BadRequest($"Model validation failed: {string.Join(", ", errors)}");
            }

            try
            {
                TesTask tes;

                // ── RAW mode ─────────────────────────────────────────────────────────
                if (Mode == "Raw")
                {
                    if (string.IsNullOrWhiteSpace(model.RawInput))
                        return BadRequest("Raw JSON input is required in Raw mode.");

                    tes = JsonConvert.DeserializeObject<TesTask>(model.RawInput)
                          ?? throw new InvalidOperationException("Failed to deserialise TES JSON.");
                }
                // ── SIMPLE or CUSTOM modes ────────────────────────────────────────────
                else if (Mode == "Simple" || Mode == "Custom")
                {
                    // 1. Resolve project
                    var project = await _clientHelper.CallAPIWithoutModel<Project?>(
                        "/api/Project/GetProject/",
                        new Dictionary<string, string> { { "projectId", model.ProjectId.ToString() } });

                    // 2. Resolve TRE list (fall back to all TREs in the project)
                    var selectedTres = model.TreRadios?
                        .Where(x => x.IsSelected)
                        .Select(x => x.Name)
                        .ToList() ?? new List<string>();

                    if (selectedTres.Count == 0)
                    {
                        return BadRequest("No Tres selected.");
                    }

                    var listOfTre = string.Join("|", selectedTres);

                    // 3. Common TES metadata
                    var tesName        = string.IsNullOrWhiteSpace(model.TESName)        ? "SQL Query Task"          : model.TESName;
                    var tesDescription = string.IsNullOrWhiteSpace(model.TESDescription) ? "Federated analysis task" : model.TESDescription;

                    // 4. Build executors depending on mode
                    List<TesExecutor> executors;

                    if (Mode == "Simple")
                    {
                        if (string.IsNullOrWhiteSpace(model.Query))
                            return BadRequest("A SQL query is required in Simple mode.");

                        var query = NormaliseText(model.Query);

                        executors = new List<TesExecutor>
                        {
                            new TesExecutor
                            {
                                Image   = _URLSettingsFrontEnd.QueryImageSQL,
                                Command = new List<string>
                                {
                                    "--Output=/outputs/output.csv",
                                    $"--Query={query}"
                                },
                                Workdir = "/app",
                                Stdin   = null,
                                Stdout  = null,
                                Stderr  = null,
                                Env     = new Dictionary<string, string>()
                            }
                        };
                    }
                    else // Custom
                    {
                        if (string.IsNullOrWhiteSpace(CustomExecutors) || CustomExecutors == "null")
                            return BadRequest("Executors data is required in Custom mode.");

                        var executorDtos = JsonConvert.DeserializeObject<List<ExecutorsV2>>(CustomExecutors)
                                           ?? new List<ExecutorsV2>();

                        executors = executorDtos
                            .Where(ex => !string.IsNullOrEmpty(ex.Image))
                            .Select(ex => new TesExecutor
                            {
                                Image   = ex.Image,
                                Command = ex.Command?
                                    .Select(NormaliseText)
                                    .ToList() ?? new List<string>(),
                                Workdir = "/app",
                                Stdin   = null,
                                Stdout  = null,
                                Stderr  = null,
                                Env     = ParseEnvList(ex.ENV)
                            })
                            .ToList();

                        if (executors.Count == 0)
                            return BadRequest("At least one valid executor with an image is required in Custom mode.");
                    }

                    // 5. Assemble the TES message (same shape for both Simple and Custom)
                    tes = new TesTask
                    {
                        State       = 0,
                        Name        = tesName,
                        Description = tesDescription,
                        Inputs      = null,
                        Outputs     = new List<TesOutput>
                        {
                            new TesOutput
                            {
                                Name        = "workdir",
                                Description = "Analysis output",
                                Url         = "s3://",
                                Path        = "/outputs",
                                Type        = TesFileType.DIRECTORYEnum
                            }
                        },
                        Resources    = null,
                        Executors    = executors,
                        Volumes      = null,
                        Tags         = new Dictionary<string, string>
                        {
                            { "Project", project?.Name ?? string.Empty },
                            { "tres",    listOfTre }
                        },
                        Logs         = null,
                        CreationTime = null
                    };
                }
                else
                {
                    return BadRequest($"Unknown submission mode: '{Mode}'. Expected Simple, Custom, or Raw.");
                }

                // ── Submit to TES API ─────────────────────────────────────────────────
                var authContext = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                await _IKeyCloakService.RefreshUserToken(authContext);
                await _clientHelper.CallAPI<TesTask, TesTask?>("/v1/tasks", tes);

                return Ok();
            }
            catch (Exception e)
            {
                Log.Error(e, "Exception in {Function}");
                return BadRequest(e.Message);
            }
        }

        // ── Private helpers ───────────────────────────────────────────────────────────

        /// <summary>
        /// Replaces literal escape sequences (\n, \r\n, \r) with real newline characters.
        /// This handles text that arrives as JSON-encoded strings with visible backslash-n.
        /// </summary>
        private static string NormaliseText(string text) =>
            text.Replace("\\r\\n", "\n")
                .Replace("\\r",    "\n")
                .Replace("\\n",    "\n");

        /// <summary>
        /// Converts a list of "KEY=value" strings into a dictionary.
        /// </summary>
        private static Dictionary<string, string> ParseEnvList(IEnumerable<string>? envLines)
        {
            var result = new Dictionary<string, string>();
            if (envLines == null) return result;

            foreach (var line in envLines)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                var parts = line.Split('=', 2);
                if (parts.Length == 2)
                    result[parts[0].Trim()] = parts[1].Trim();
            }

            return result;
        }
    }
}
