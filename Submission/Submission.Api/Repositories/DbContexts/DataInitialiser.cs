using Newtonsoft.Json;
using Serilog;
using FiveSafesTes.Core.Models;
using FiveSafesTes.Core.Models.Enums;
using FiveSafesTes.Core.Models.Tes;
using FiveSafesTes.Core.Models.ViewModels;
using FiveSafesTes.Core.Services;
using Submission.Api.Services.Contract;

namespace Submission.Api.Repositories.DbContexts
{
    public class DataInitialiser
    {
        private readonly MinioSettings _minioSettings;
        private readonly ApplicationDbContext _dbContext;
        private readonly IKeycloakTokenApiHelper _keycloackTokenApiHelper;
        private readonly IKeycloakMinioUserService _userService;
        private readonly IMinioHelper _minioHelper;

        public DataInitialiser(MinioSettings minioSettings, ApplicationDbContext dbContext,
            IKeycloakTokenApiHelper keycloackTokenApiHelper, IKeycloakMinioUserService userService,
            IMinioHelper minioHelper)
        {
            _minioSettings = minioSettings;
            _dbContext = dbContext;
            _keycloackTokenApiHelper = keycloackTokenApiHelper;
            _userService = userService;
            _minioHelper = minioHelper;
        }

        public void SeedAllInOneData()
        {
            try
            {
                var trename = "DEMO";
                var tre = _dbContext.Tres.FirstOrDefault(x => x.Name.ToLower() == "D".ToLower());
                if (tre == null)
                {
                    var demo = CreateTre(trename, "accessfromtretosubmission");
                    var globaladmin = CreateUser("globaladminuser", "globaladminuser@example.com");
                    var testing = CreateProject("Testing");
                    AddMissingTre(testing, demo);
                    AddMissingUser(testing, globaladmin);
                    _dbContext.SaveChanges();
                }
            }
            catch (Exception e)
            {
                Log.Error(e, "{Function} Error seeding data", "SeedAllInOneData");
                throw;
            }
        }

        private Project CreateProject(string name)
        {
            var proj = _dbContext.Projects.FirstOrDefault(x => x.Name.ToLower() == name.ToLower());

            if (proj == null)
            {
                proj = new Project()
                {
                    Name = name,
                    Display = name,
                    EndDate = DateTime.Now.ToUniversalTime().AddYears(3),
                    StartDate = DateTime.Now.ToUniversalTime(),
                    SubmissionBucket = "",
                    OutputBucket = "",
                    Tres = new List<Tre>(),
                    Users = new List<User>(),
                    Submissions = new List<FiveSafesTes.Core.Models.Submission>(),
                    ProjectDescription = ""
                };

                proj.FormData = JsonConvert.SerializeObject(proj);
                // Add and save to get a permanent Id from the database
                _dbContext.Projects.Add(proj);
                _dbContext.SaveChanges();

                try
                {
                    // Now proj.Id has a permanent value; generate stable bucket names
                    var submission = GenerateRandomName(proj.Id.ToString()) + "submission".Replace("_", "");
                    var output = GenerateRandomName(proj.Id.ToString()) + "output".Replace("_", "");

                    proj.SubmissionBucket = submission;
                    proj.OutputBucket = output;
                    // Create submission bucket
                    var submissionBucket = _minioHelper.CreateBucket(submission.ToLower()).Result;
                    var submissionBucketPolicy = _minioHelper.CreateBucketPolicy(submission.ToLower()).Result;
                    // Create output bucket
                    var outputBucket = _minioHelper.CreateBucket(output.ToLower()).Result;
                    var outputBucketPolicy = _minioHelper.CreateBucketPolicy(output.ToLower()).Result;

                    // tracked entity updated, persist changes
                    _dbContext.SaveChanges();

                    Log.Information(
                        "Created project {Project} with submission bucket {Submission} and output bucket {Output}",
                        proj.Name, proj.SubmissionBucket, proj.OutputBucket);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error creating Minio buckets for project {Project}", proj.Name);
                    throw;
                }
            }

            return proj;
        }


        private User CreateUser(string name, string email)
        {
            var user = _dbContext.Users.FirstOrDefault(x => x.Name.ToLower() == name.ToLower());
            if (user == null)
            {
                user = new User()
                {
                    Name = name,
                    Email = email
                };
                user.FormData = JsonConvert.SerializeObject(user);
                _dbContext.Users.Add(user);
            }

            return user;
        }

        private Tre CreateTre(string name, string adminUser)
        {
            var tre = _dbContext.Tres.FirstOrDefault(x => x.Name.ToLower() == name.ToLower());
            if (tre == null)
            {
                tre = new Tre()
                {
                    Name = name,
                    AdminUsername = adminUser,
                    About = ""
                };
                tre.FormData = JsonConvert.SerializeObject(tre);
                _dbContext.Tres.Add(tre);
            }

            return tre;
        }

        private void AddMissingTre(Project project, Tre tre)
        {
            if (!project.Tres.Contains(tre))
            {
                project.Tres.Add(tre);
            }
        }

        private void AddMissingUser(Project project, User user)
        {
            if (!project.Users.Contains(user))
            {
                //Test User for Testing
                var accessToken = _keycloackTokenApiHelper.GetTokenForUser("minioadmin", "password123", "").Result;
                var attributeName = _minioSettings.AttributeName;

                var submissionUserAttribute = _userService.SetMinioUserAttribute(accessToken, user.Name.ToString(),
                    attributeName, project.SubmissionBucket.ToLower() + "_policy").Result;
                var outputUserAttribute = _userService.SetMinioUserAttribute(accessToken, user.Name.ToString(),
                    attributeName, project.OutputBucket.ToLower() + "_policy").Result;

                project.Users.Add(user);
            }
        }

        private void AddSubmission(string name, string project, string username, string treStr)
        {
            try
            {
                if (_dbContext.Submissions.Any(x => x.TesName.ToLower() == name.ToLower()))
                {
                    return;
                }

                string template = "{" +
                                  "\"id\":null," +
                                  "\"state\":0," +
                                  "\"name\":\"{name}\"," +
                                  "\"description\":null," +
                                  "\"inputs\":null," +
                                  "\"outputs\":null," +
                                  "\"resources\":null," +
                                  "\"executors\":[{" +
                                  "\"image\":\"\\\\\\\\minio\\\\justin1.crate\"," +
                                  "\"command\":null," +
                                  "\"workdir\":null," +
                                  "\"stdin\":null," +
                                  "\"stdout\":null," +
                                  "\"stderr\":null," +
                                  "\"env\":null" +
                                  "}]," +
                                  "\"volumes\":null," +
                                  "\"tags\":{\"project\":\"{project}\",\"tres\":\"{tres}\"}," +
                                  "\"logs\":null," +
                                  "\"creation_time\":null" +
                                  "}";

                var tesString = template.Replace("{name}", name).Replace("{project}", project)
                    .Replace("{tres}", treStr);
                var tesTask = JsonConvert.DeserializeObject<TesTask>(tesString);
                var dbProject = _dbContext.Projects.First(x => x.Name.ToLower() == project.ToLower());
                var user = _dbContext.Users.First(x => x.Name.ToLower() == username.ToLower());
                var sub = new FiveSafesTes.Core.Models.Submission()
                {
                    DockerInputLocation = tesTask.Executors.First().Image,
                    Project = dbProject,
                    Status = StatusType.WaitingForChildSubsToComplete,
                    StartTime = DateTime.Now.ToUniversalTime(),
                    LastStatusUpdate = DateTime.Now.ToUniversalTime(),
                    SubmittedBy = user,
                    TesName = tesTask.Name,
                    HistoricStatuses = new List<HistoricStatus>(),
                    SourceCrate = tesTask.Executors.First().Image,
                };


                _dbContext.Submissions.Add(sub);
                _dbContext.SaveChanges();
                tesTask.Id = sub.Id.ToString();
                sub.TesId = tesTask.Id;
                var newTesString = JsonConvert.SerializeObject(tesTask);
                sub.TesJson = newTesString;
                _dbContext.SaveChanges();


                List<string> tres = new List<string>();
                if (!string.IsNullOrWhiteSpace(treStr))
                {
                    tres = treStr.Split('|').Select(x => x.ToLower()).ToList();
                }


                var dbTres = new List<Tre>();

                if (tres.Count == 0)
                {
                    dbTres = dbProject.Tres;
                }
                else
                {
                    foreach (var tre in tres)
                    {
                        dbTres.Add(dbProject.Tres.First(x => x.Name.ToLower() == tre.ToLower()));
                    }
                }

                foreach (var tre in dbTres)
                {
                    _dbContext.Add(new FiveSafesTes.Core.Models.Submission()
                    {
                        DockerInputLocation = tesTask.Executors.First().Image,
                        Project = dbProject,
                        Status = StatusType.WaitingForAgentToTransfer,
                        StartTime = DateTime.Now.ToUniversalTime(),
                        LastStatusUpdate = DateTime.Now.ToUniversalTime(),
                        SubmittedBy = sub.SubmittedBy,
                        Parent = sub,
                        TesId = tesTask.Id,
                        TesJson = sub.TesJson,
                        HistoricStatuses = new List<HistoricStatus>(),
                        Tre = tre,
                        TesName = tesTask.Name,
                        SourceCrate = tesTask.Executors.First().Image,
                    });
                }

                _dbContext.SaveChanges();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }

        private string GenerateRandomName(string prefix)
        {
            Random random = new Random();
            string randomName = prefix + random.Next(1000, 9999);
            return randomName;
        }
    }
}
