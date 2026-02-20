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
                        var uplodaResultTest =
                            await _clientHelper.CallAPIToSendFile<APIReturn>("/api/Project/UploadToMinio", "file",
                                model.File, paramss);
                    }

                    var minioEndpoint =
                        await _clientHelper.CallAPIWithoutModel<MinioEndpoint>("/api/Project/GetMinioEndPoint");
                    //Don't add http:// minioEndpoint.Url already has it. And if not it should!
                    imageUrl = /*"http://" +*/ minioEndpoint.Url + "/browser/" + project.SubmissionBucket + "/" +
                                               model.File.FileName;
                }

                var TesTask = new TesTask()
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


                var result = await _clientHelper.CallAPI<TesTask, TesTask?>("/v1/tasks", TesTask);

                return RedirectToAction("GetASubmission", new { id = result.Id });
                //return Ok();
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

            List<FiveSafesTes.Core.Models.Submission> displaySubmissionsList = new List<FiveSafesTes.Core.Models.Submission>();
            var res = _clientHelper.CallAPIWithoutModel<List<FiveSafesTes.Core.Models.Submission>>("/api/Submission/GetAllSubmissions/").Result
                .Where(x => x.Parent == null).ToList();

            res = res.Where(x => x.Parent == null).ToList();


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
            var test = new SubmissionInfo()
            {
                Submission = res,
                Stages = _clientHelper.CallAPIWithoutModel<Stages>("/api/Submission/StageTypes/").Result
            };
            return View(test);
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
        public async Task<ActionResult> SubmissionWizardAction(SubmissionWizardV2 model, string Executors, string SQL)
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
    
    }
}
