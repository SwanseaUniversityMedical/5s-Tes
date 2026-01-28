using System.Text;
using Agent.Api.Repositories.DbContexts;
using EasyNetQ;
using FiveSafesTes.Core.Models;
using FiveSafesTes.Core.Models.Enums;
using FiveSafesTes.Core.Models.ViewModels;
using FiveSafesTes.Core.Rabbit;
using FiveSafesTes.Core.Services;
using Newtonsoft.Json;
using Serilog;

namespace Agent.Api.Services
{
  public class ConsumeInternalMessageService : BackgroundService
  {
    private readonly IBus _bus;
    private readonly ApplicationDbContext _dbContext;
    private readonly IMinioTreHelper _minioTreHelper;
    private readonly IDareClientWithoutTokenHelper _dareHelper;
    private readonly IMinioSubHelper _minioSubHelper;
    private readonly ISubmissionHelper _subHelper;
    private readonly string _treName;


    public ConsumeInternalMessageService(IBus bus, IServiceProvider serviceProvider)
    {
      _bus = bus;
      _dbContext = serviceProvider.CreateScope().ServiceProvider.GetRequiredService<ApplicationDbContext>();
      _minioTreHelper = serviceProvider.CreateScope().ServiceProvider.GetRequiredService<IMinioTreHelper>();
      _minioSubHelper = serviceProvider.CreateScope().ServiceProvider.GetRequiredService<IMinioSubHelper>();
      _dareHelper = serviceProvider.CreateScope().ServiceProvider.GetRequiredService<IDareClientWithoutTokenHelper>();
      _subHelper = serviceProvider.CreateScope().ServiceProvider.GetRequiredService<ISubmissionHelper>();
      var config = serviceProvider.CreateScope().ServiceProvider.GetRequiredService<IConfiguration>();
      _treName = config["TreName"];
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
      try
      {
        //Consume All Queue
        //var fetch = await _bus.Advanced.QueueDeclareAsync(QueueConstants.FetchExternalFile);
        //_bus.Advanced.Consume<MQFetchFile>(fetch, Process);
        var finalOutput = await _bus.Advanced.QueueDeclareAsync(QueueConstants.ProcessFinalOutput);
        _bus.Advanced.Consume<FinalOutcome>(finalOutput, ProcessFinalOutcome);
      }
      catch (Exception e)
      {
        Log.Error("{Function} ConsumeProcessForm:- Failed to subscribe due to error: {e}", "ExecuteAsync", e.Message);
      }
    }

    public void Test()
    {
    }


    private void ProcessFinalOutcome(IMessage<FinalOutcome> message, MessageReceivedInfo info)
    {
      try
      {
        var outcome = message.Body;
        var submission = _dareHelper
          .CallAPIWithoutModel<Submission>($"/api/Submission/GetASubmission/{outcome.SubId}")
          .Result;
        var sourceBucket = _subHelper.GetOutputBucketGuts(outcome.SubId, false, false);


        var paramlist2 = new Dictionary<string, string>();
        paramlist2.Add("projectId", submission.Project.Id.ToString());
        var project = _dareHelper.CallAPIWithoutModel<Project?>(
          "/api/Project/GetProject/", paramlist2).Result;

        var destinationBucket = project.OutputBucket;

        //Copy file to output bucket
        var source = _minioTreHelper.GetCopyObject(sourceBucket.Bucket, outcome.File).Result;


        var destfile = sourceBucket.Path + _treName + "/" + outcome.File.Replace(sourceBucket.Path, "");
        var copyResult =
          _minioSubHelper.CopyObjectToDestination(destinationBucket, destfile, source);

        var StatusResult = _subHelper.CloseSubmissionForTre(outcome.SubId, StatusType.Completed, "", destfile);
      }
      catch (Exception e)
      {
        _subHelper.CloseSubmissionForTre(message.Body.SubId, StatusType.Failed, e.Message, "");
        Log.Error(e, "{Function} Error", "ProcessFinalOutcome");
        throw;
      }
    }


    private T ConvertByteArrayToType<T>(byte[] byteArray)
    {
      return JsonConvert.DeserializeObject<T>(Encoding.UTF8.GetString(byteArray));
    }
  }
}
