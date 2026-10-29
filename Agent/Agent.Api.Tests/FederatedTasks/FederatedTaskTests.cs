using FiveSafesTes.Core.Models.FederatedTasks;
using FiveSafesTes.Core.Models.Tes;
using Newtonsoft.Json;
using Xunit;

namespace Agent.Api.Tests.FederatedTasks;

public class FederatedTaskTests
{
    private static readonly Guid TaskId = Guid.Parse("11111111-2222-3333-4444-555555555555");
    private static readonly DateTime Submitted = new(2026, 10, 26, 9, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Completed = new(2026, 10, 26, 9, 5, 0, DateTimeKind.Utc);

    private static FederatedTask Make(TesState state)
    {
        var tes = new TesTask
        {
            Executors = [new TesExecutor { Image = "tre", Command = ["mean", "age"] }]
        };
        return new FederatedTask
        {
            TaskId = TaskId,
            StudyId = "study",
            Operation = "mean",
            Dataset = "cohort",
            TesTaskJson = JsonConvert.SerializeObject(tes),
            State = state,
            SubmittedAt = Submitted
        };
    }

    [Fact]
    public void ToTesTask_Queued_HasNoLogs()
    {
        var task = Make(TesState.QUEUEDEnum).ToTesTask();

        Assert.Equal(TaskId.ToString(), task.Id);
        Assert.Equal(TesState.QUEUEDEnum, task.State);
        Assert.Null(task.Logs);
        Assert.Equal(new DateTimeOffset(Submitted, TimeSpan.Zero), task.CreationTime);
        Assert.Equal(["mean", "age"], task.Executors![0].Command);
    }

    [Fact]
    public void ToTesTask_Complete_HasValueMetadata()
    {
        var entity = Make(TesState.COMPLETEEnum);
        entity.CompletedAt = Completed;
        entity.ResultValue = 40.5m;
        entity.ResultRowCount = 2;

        var log = Assert.Single(entity.ToTesTask().Logs!);

        Assert.Equal("40.5", log.Metadata!["value"]);
        Assert.Equal("2", log.Metadata["row_count"]);
        Assert.Equal(new DateTimeOffset(Completed, TimeSpan.Zero), log.EndTime);
    }

    [Fact]
    public void ToTesTask_ExecutorError_HasErrorMetadata()
    {
        var entity = Make(TesState.EXECUTORERROREnum);
        entity.CompletedAt = Completed;
        entity.ErrorCode = "dataset_not_found";
        entity.ErrorMessage = "no such dataset";

        var log = Assert.Single(entity.ToTesTask().Logs!);

        Assert.Equal("dataset_not_found", log.Metadata!["error_code"]);
        Assert.Equal("no such dataset", log.Metadata["error_message"]);
        Assert.False(log.Metadata.ContainsKey("value"));
    }
}
