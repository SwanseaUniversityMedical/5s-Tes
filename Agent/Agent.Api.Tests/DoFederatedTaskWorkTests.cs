using Agent.Api.FederatedTasks;
using Agent.Api.Models;
using Agent.Api.Tests.FederatedTasks;
using FiveSafesTes.Core.Models.FederatedTasks;
using FiveSafesTes.Core.Models.Tes;
using Xunit;

namespace Agent.Api.Tests;

public class DoFederatedTaskWorkTests
{
    private const string Csv = "person_id,age,sex\n1,34,F\n2,51,M\n3,47,F\n";

    private sealed class FakeClient : IFederatedTaskClient
    {
        public bool IsConfigured { get; set; } = true;
        public List<TesTask> Pending { get; } = new();
        public List<FederatedTaskResultReport> Reports { get; } = new();
        public int GetPendingCalls { get; private set; }

        public Task<List<TesTask>> GetPendingAsync()
        {
            GetPendingCalls++;
            return Task.FromResult(Pending);
        }

        public Task<bool> ReportAsync(FederatedTaskResultReport report)
        {
            Reports.Add(report);
            return Task.FromResult(true);
        }
    }

    private static FakeDatasetStore Store() => new FakeDatasetStore().Add("dataset/cohort_2024/cohort.csv", Csv);

    private static TesTask PendingTask(string id, string dataset, string operation, string? column, params TaskFilter[] filters)
    {
        var task = FederatedTesProfile.Build(new FederatedTaskSpec(operation, dataset, column, filters, "study-1", "tre-a"));
        task.Id = id;
        return task;
    }

    private static DoFederatedTaskWork Work(FakeClient client, FakeDatasetStore store) =>
        new(client, new DatasetReader(store, new FederatedTaskSettings()));

    [Fact]
    public async Task Execute_ValidMean_ReportsComplete()
    {
        var client = new FakeClient();
        client.Pending.Add(PendingTask("t1", "cohort_2024", "mean", "age", new TaskFilter("sex", "eq", "F")));

        await Work(client, Store()).Execute();

        var report = Assert.Single(client.Reports);
        Assert.Equal("t1", report.TaskId);
        Assert.Equal(TesState.COMPLETEEnum, report.State);
        Assert.Equal(40.5m, report.Value);
        Assert.Equal(2, report.RowCount);
    }

    [Fact]
    public async Task Execute_MissingDataset_ReportsExecutorErrorDatasetNotFound()
    {
        var client = new FakeClient();
        client.Pending.Add(PendingTask("t1", "nope", "count", null));

        await Work(client, Store()).Execute();

        var report = Assert.Single(client.Reports);
        Assert.Equal(TesState.EXECUTORERROREnum, report.State);
        Assert.Equal("dataset_not_found", report.ErrorCode);
    }

    [Fact]
    public async Task Execute_MissingColumn_ReportsExecutorErrorColumnNotFound()
    {
        var client = new FakeClient();
        client.Pending.Add(PendingTask("t1", "cohort_2024", "mean", "height"));

        await Work(client, Store()).Execute();

        var report = Assert.Single(client.Reports);
        Assert.Equal(TesState.EXECUTORERROREnum, report.State);
        Assert.Equal("column_not_found", report.ErrorCode);
    }

    [Fact]
    public async Task Execute_TaskOutsideProfile_ReportsInvalidTask()
    {
        var client = new FakeClient();
        var task = PendingTask("t1", "cohort_2024", "count", null);
        task.Executors[0].Image = "ubuntu:22.04";
        client.Pending.Add(task);

        await Work(client, Store()).Execute();

        var report = Assert.Single(client.Reports);
        Assert.Equal(TesState.EXECUTORERROREnum, report.State);
        Assert.Equal("invalid_task", report.ErrorCode);
    }

    [Fact]
    public async Task Execute_OneTaskThrows_OthersStillReported()
    {
        var store = Store().Add("dataset/other/other.csv", "a\n1\n");
        store.ThrowOnOpenKey = "dataset/other/other.csv";
        var client = new FakeClient();
        client.Pending.Add(PendingTask("t1", "other", "count", null));
        client.Pending.Add(PendingTask("t2", "cohort_2024", "count", null));

        await Work(client, store).Execute();

        Assert.Equal(2, client.Reports.Count);
        Assert.Equal(TesState.SYSTEMERROREnum, client.Reports[0].State);
        Assert.Equal("internal_error", client.Reports[0].ErrorCode);
        Assert.Contains("cannot open dataset/other/other.csv", client.Reports[0].ErrorMessage);
        Assert.Equal(TesState.COMPLETEEnum, client.Reports[1].State);
    }

    [Fact]
    public async Task Execute_NotConfigured_DoesNothing()
    {
        var client = new FakeClient { IsConfigured = false };
        client.Pending.Add(PendingTask("t1", "cohort_2024", "count", null));

        await Work(client, Store()).Execute();

        Assert.Equal(0, client.GetPendingCalls);
        Assert.Empty(client.Reports);
    }
}
