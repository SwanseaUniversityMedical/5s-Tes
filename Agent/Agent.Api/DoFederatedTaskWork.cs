using Agent.Api.FederatedTasks;
using FiveSafesTes.Core.Models.FederatedTasks;
using FiveSafesTes.Core.Models.Tes;
using Hangfire;
using Serilog;

namespace Agent.Api
{
    public interface IDoFederatedTaskWork
    {
        [AutomaticRetry(Attempts = 0)]
        Task Execute();
    }

    /// <summary>
    /// Polls Submission for pending federated tasks, runs each against its dataset and reports the result.
    /// </summary>
    public class DoFederatedTaskWork : IDoFederatedTaskWork
    {
        private readonly IFederatedTaskClient _client;
        private readonly IDatasetReader _reader;

        public DoFederatedTaskWork(IFederatedTaskClient client, IDatasetReader reader)
        {
            _client = client;
            _reader = reader;
        }

        public async Task Execute()
        {
            // Don't execute if no credentials have been uploaded.
            if (!_client.IsConfigured) return;

            var pending = await _client.GetPendingAsync() ?? new List<TesTask>();
            if (pending.Count == 0) return;

            Log.Information("Federated tasks: {Count} pending task(s)", pending.Count);

            foreach (var task in pending)
            {
                var report = await RunAsync(task);
                try
                {
                    // A task whose report is lost stays QUEUED on the server and is retried on the next poll.
                    if (!await _client.ReportAsync(report))
                    {
                        Log.Warning("Federated tasks: Submission did not accept the result for task {TaskId}", report.TaskId);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Federated tasks: failed to report the result for task {TaskId}", report.TaskId);
                }
            }
        }

        private async Task<FederatedTaskResultReport> RunAsync(TesTask task)
        {
            var taskId = task.Id ?? string.Empty;
            try
            {
                // Enqueue refuses tasks outside the profile, so a failure here is defence in depth.
                var parsed = FederatedTesProfile.Parse(task);
                if (!parsed.IsSuccess)
                    return Error(taskId, parsed.ErrorCode!, parsed.ErrorMessage);

                var dataset = await _reader.ReadAsync(parsed.Spec!.Dataset, CancellationToken.None);
                if (!dataset.IsSuccess)
                    return Error(taskId, dataset.ErrorCode!, dataset.ErrorMessage);

                var result = FederatedOperationExecutor.Execute(parsed.Spec, dataset.Table!);
                if (!result.IsSuccess)
                    return Error(taskId, result.ErrorCode!, result.ErrorMessage);

                return new FederatedTaskResultReport
                {
                    TaskId = taskId,
                    State = TesState.COMPLETEEnum,
                    Value = result.Value,
                    RowCount = result.RowCount
                };
            }
            catch (Exception ex)
            {
                // The dataset reader lets store failures propagate; report them rather than fail the whole poll.
                Log.Error(ex, "Federated tasks: task {TaskId} failed unexpectedly", taskId);
                return new FederatedTaskResultReport
                {
                    TaskId = taskId,
                    State = TesState.SYSTEMERROREnum,
                    ErrorCode = "internal_error",
                    ErrorMessage = ex.Message
                };
            }
        }

        private static FederatedTaskResultReport Error(string taskId, string code, string? message)
        {
            Log.Information("Federated tasks: task {TaskId} failed with {ErrorCode}", taskId, code);
            return new FederatedTaskResultReport
            {
                TaskId = taskId,
                State = TesState.EXECUTORERROREnum,
                ErrorCode = code,
                ErrorMessage = message
            };
        }
    }
}
