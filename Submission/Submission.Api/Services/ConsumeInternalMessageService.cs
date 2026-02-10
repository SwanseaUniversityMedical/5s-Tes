using EasyNetQ;
using Newtonsoft.Json;
using Serilog;
using System.Text;
using FiveSafesTes.Core.Models;
using FiveSafesTes.Core.Models.Enums;
using FiveSafesTes.Core.Models.Tes;
using FiveSafesTes.Core.Models.ViewModels;
using FiveSafesTes.Core.Rabbit;
using FiveSafesTes.Core.Services;
using Submission.Api.Repositories.DbContexts;

namespace Submission.Api.Services
{
    public class ConsumeInternalMessageService : BackgroundService
    {
        private readonly IBus _bus;
        private readonly ApplicationDbContext _dbContext;
        private readonly MinioSettings _minioSettings;
        private readonly IMinioHelper _minioHelper;

        public ConsumeInternalMessageService(IBus bus, IServiceProvider serviceProvider)
        {
            _bus = bus;
            _dbContext = serviceProvider.CreateScope().ServiceProvider.GetRequiredService<ApplicationDbContext>();
            _minioSettings = serviceProvider.CreateScope().ServiceProvider.GetRequiredService<MinioSettings>();
            _minioHelper = serviceProvider.CreateScope().ServiceProvider.GetRequiredService<IMinioHelper>();; 

        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                //Consume All Queue
                var subs = await _bus.Advanced.QueueDeclareAsync(QueueConstants.ProcessSub);
                _bus.Advanced.Consume<int>(subs, Process);

                //var fetch = await _bus.Advanced.QueueDeclareAsync(QueueConstants.FetchExternalFile);
                //_bus.Advanced.Consume<byte[]>(fetch, ProcessFetchExternal);
            }
            catch (Exception e)
            {
                Log.Error("{Function} ConsumeProcessForm:- Failed to subscribe due to error: {e}", "ExecuteAsync", e.Message);

            }
        }


        //Implement proper check
        private bool ValidateCreate(FiveSafesTes.Core.Models.Submission sub)
        {
            return true;
        }

        private void Process(IMessage<int> message, MessageReceivedInfo info)
        {
            try
            {
                var sub = _dbContext.Submissions.First(s => s.Id == message.Body);

                try
                {
                    
              

               
                

                var messageMQ = new MQFetchFile();
                messageMQ.OriginalUrl = sub.DockerInputLocation;
                messageMQ.Url = sub.SourceCrate;
                messageMQ.BucketName = sub.Project.SubmissionBucket;

                    Uri uri = null;

                    try
                    {
                        uri = new Uri(sub.DockerInputLocation);
                    }
                    catch (Exception ex)
                    {

                    }
                    Log.Information("{Function} Crate loc {Crate}", "Process", sub.DockerInputLocation);
                    if (uri != null)
                    {
                        
                        string fileName = Path.GetFileName(uri.LocalPath);
                        Log.Information("{Function} Full file loc {File}, Incoming URL {URL}, our minio {Minio}", "Process", fileName, uri.Scheme.ToLower() + "://" + uri.Host.ToLower() + ":" + uri.Port, _minioSettings.AdminConsole.ToLower());
                        messageMQ.Key = fileName;
                        if (uri.Scheme.ToLower() + "://" + uri.Host.ToLower() + ":" + uri.Port != _minioSettings.AdminConsole.ToLower())
                        {
                            Log.Information("{Function} Copying external", "Process");
                            _minioHelper.RabbitExternalObject(messageMQ);


                            var minioEndpoint = new MinioEndpoint()
                            {
                                Url = _minioSettings.AdminConsole,
                            };

                            //Do not add http. It should already have it
                            messageMQ.Url = /*"http://" +*/ minioEndpoint.Url + "/browser/" + messageMQ.BucketName + "/" + messageMQ.Key;
                            Log.Information("{Function} New url {URL}", "Process", messageMQ.Url);
                        }
                    }
                   

                    UpdateSubmissionStatus.UpdateStatusNoSave(sub, StatusType.SubmissionWaitingForCrateFormatCheck, "");
                if (ValidateCreate(sub))
                {
                    UpdateSubmissionStatus.UpdateStatusNoSave(sub, StatusType.SubmissionCrateValidated, "");
                }
                else
                {
                    UpdateSubmissionStatus.UpdateStatusNoSave(sub, StatusType.SubmissionCrateValidationFailed, "");
                    UpdateSubmissionStatus.UpdateStatusNoSave(sub, StatusType.Failed, "");
                }

                var dbproj = sub.Project;
                var tesTask = JsonConvert.DeserializeObject<TesTask>(sub.TesJson);

                var trestr = tesTask.Tags.Where(x => x.Key.ToLower() == "tres").Select(x => x.Value).FirstOrDefault();
                List<string> tres = new List<string>();
                if (!string.IsNullOrWhiteSpace(trestr))
                {
                    tres = trestr.Split('|').Select(x => x.ToLower()).ToList();
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
                UpdateSubmissionStatus.UpdateStatusNoSave(sub, StatusType.WaitingForChildSubsToComplete, "");

                foreach (var tre in dbtres)
                {
                    _dbContext.Add(new FiveSafesTes.Core.Models.Submission()
                    {
                        DockerInputLocation = messageMQ.Url,
                        Project = dbproj,
                        StartTime = DateTime.Now.ToUniversalTime(),
                        Status = StatusType.WaitingForAgentToTransfer,
                        LastStatusUpdate = DateTime.Now.ToUniversalTime(),
                        SubmittedBy = sub.SubmittedBy,
                        Parent = sub,
                        TesId = tesTask.Id,
                        TesJson = sub.TesJson,
                        Tre = tre,
                        TesName = tesTask.Name,
                        SourceCrate = tesTask.Executors.First().Image,
                    });

                }

                _dbContext.SaveChanges();
                Log.Information("{Function} Processed sub for {id}", "Process", message.Body);
                }
                catch (Exception e)
                {
                    UpdateSubmissionStatus.UpdateStatusNoSave(sub, StatusType.Failed, e.Message);
                    _dbContext.SaveChanges();

                    
                    
                    throw;
                }

            }
            catch (Exception ex)
            {
                Log.Error(ex, "{Function} failed to process.", "Process");
                throw;
            }
        }

        //private async Task ProcessFetchExternal(IMessage<byte[]> msgBytes,   MessageReceivedInfo info )
        //{
        //    try
        //    {
        //        var message = Encoding.UTF8.GetString(msgBytes.Body);
        //        await _minioHelper.RabbitExternalObject(JsonConvert.DeserializeObject<MQFetchFile>(message));
        //    }
        //    catch (Exception e)
        //    {

        //        throw;
        //    }
        //}




        private T ConvertByteArrayToType<T>(byte[] byteArray)
        {
            return JsonConvert.DeserializeObject<T>(Encoding.UTF8.GetString(byteArray));
        }
    }
}
