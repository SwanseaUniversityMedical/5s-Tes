using Agent.Api.Models;
using Agent.Api.Repositories.DbContexts;
using Agent.Api.Services;
using FiveSafesTes.Core.Models.Enums;
using FiveSafesTes.Core.Models.Settings;
using FiveSafesTes.Core.Services;
using Hangfire;
using Serilog;

namespace Agent.Api;

public interface IDoAirlockSendRetryWork
{
  // One run at a time: a slow run (e.g. a large resend) must not overlap the next one.
  [AutomaticRetry(Attempts = 0)]
  [DisableConcurrentExecution(timeoutInSeconds: 600)]
  Task Execute();
}

/// <summary>
/// Resends outputs whose send to Airlock failed (only scheduled when EgressSettings:Provider is Airlock).
/// </summary>
public class DoAirlockSendRetryWork(ApplicationDbContext dbContext, AirlockEgressProvider airlockEgressProvider, ISubmissionHelper subHelper,AirlockSettings airlockSettings): IDoAirlockSendRetryWork
{
  public async Task Execute()
  {
    var failed = dbContext.AirlockEgressRequests
      .Where(x => x.State == AirlockEgressState.SendFailed)
      .OrderBy(x => x.Id)
      .ToList();

    if (failed.Count == 0) return;

    Log.Information("{Function} Retrying {Count} failed Airlock sends", "DoAirlockSendRetryWork", failed.Count);

    foreach (var row in failed)
    {
      try
      {
        await RetrySend(row);
      }
      catch (Exception ex)
      {
        // Leave the row open so the next run tries again.
        Log.Error(ex, "{Function} Error resending sub {SubId}", "DoAirlockSendRetryWork", row.SubId);
        row.LastError = ex.Message;
        await dbContext.SaveChangesAsync();
      }
    }
  }

  private async Task RetrySend(AirlockEgressRequest row)
  {
    if (row.SendAttempts >= airlockSettings.MaxSendAttempts)
    {
      string reason = $"Sending outputs to Airlock failed after {row.SendAttempts} attempts: {row.LastError}";
      Log.Error("{Function} Giving up on sub {SubId}: {Reason}", "DoAirlockSendRetryWork", row.SubId, reason);
      subHelper.CloseSubmissionForTre(row.SubId.ToString(), StatusType.Failed, reason, "");
      row.State = AirlockEgressState.Closed;
      row.ClosedAt = DateTime.UtcNow;
      await dbContext.SaveChangesAsync();
      return;
    }

    Log.Information("{Function} Resending sub {SubId} to Airlock (attempt {Attempt})", "DoAirlockSendRetryWork", row.SubId, row.SendAttempts + 1);
    await airlockEgressProvider.ResendAsync(row);
  }
}
