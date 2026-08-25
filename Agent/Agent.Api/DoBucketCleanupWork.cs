using Agent.Api.Repositories.DbContexts;
using Agent.Api.Services;
using FiveSafesTes.Core.Models.APISimpleTypeReturns;
using FiveSafesTes.Core.Services;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace Agent.Api;

public interface IDoBucketCleanupWork
{
    [AutomaticRetry(Attempts = 0)]
    Task Execute();
}

/// <summary>
/// Deletes the S3 buckets belonging to expired projects. A project is eligible once it is archived
/// (no longer returned by the Submission layer) AND past the grace window
/// (<see cref="JobSettings.DaysAfterExpiryBeforeBucketDeletion"/>) measured from its expiry date,
/// falling back to its archive date when the expiry date is still in the future (e.g. a project
/// deleted from Submission before its end date). Deletes the TRE-owned buckets directly and asks the
/// Submission layer to delete its own buckets for the same project. Enabled via
/// <see cref="JobSettings.bucketCleanupSchedule"/> (0 disables; a non-zero value is the hour of
/// day the job runs daily), so the job is only scheduled at all when that value is non-zero.
/// </summary>
public class DoBucketCleanupWork(
    ApplicationDbContext dbContext,
    IMinioTreHelper minioTreHelper,
    IDareClientWithoutTokenHelper dareClient,
    JobSettings jobSettings)
    : IDoBucketCleanupWork
{
    public async Task Execute()
    {
        var now = DateTime.UtcNow;
        var cutoff = now.AddDays(-jobSettings.DaysAfterExpiryBeforeBucketDeletion);

        Log.Information(
            "Bucket cleanup job started (grace {Days} day(s); a project is eligible when archived and expired/archived before {Cutoff:u})",
            jobSettings.DaysAfterExpiryBeforeBucketDeletion, cutoff);

        var eligible = await dbContext.Projects
            .Where(p => p.Archived
                        && !p.BucketsCleaned
                        && (p.ProjectExpiryDate < cutoff
                            || (p.ProjectExpiryDate >= now && p.ArchivedOn != null && p.ArchivedOn < cutoff)))
            .ToListAsync();

        if (eligible.Count == 0)
        {
            Log.Information("Bucket cleanup: no expired projects eligible for deletion");
            return;
        }

        Log.Information("Bucket cleanup: {Count} expired project(s) eligible for deletion", eligible.Count);

        foreach (var project in eligible)
        {
            try
            {
                // 1. Delete the TRE-owned buckets (admin creds via the TRE object store helper).
                var treOk = true;
                foreach (var bucket in new[] { project.SubmissionBucketTre, project.OutputBucketTre })
                {
                    if (!string.IsNullOrWhiteSpace(bucket))
                    {
                        treOk &= await minioTreHelper.DeleteBucketAsync(bucket);
                    }
                }

                // 2. Ask the Submission layer to delete its own buckets for this project.
                var subResult = await dareClient.CallAPIWithoutModel<BoolReturn>(
                    $"/api/Project/CleanupBuckets/{project.SubmissionProjectId}", null, HttpMethod.Post);
                var subOk = subResult?.Result ?? false;

                if (treOk && subOk)
                {
                    project.BucketsCleaned = true;
                    project.BucketsCleanedOn = now;
                    Log.Information(
                        "Bucket cleanup completed for TreProject {ProjectId} (submissionProjectId {SubmissionProjectId})",
                        project.Id, project.SubmissionProjectId);
                }
                else
                {
                    // Leave BucketsCleaned false so the next cycle retries the parts that failed.
                    Log.Error(
                        "Bucket cleanup incomplete for TreProject {ProjectId}: treBuckets={TreOk}, submission={SubOk}. Will retry next cycle.",
                        project.Id, treOk, subOk);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Bucket cleanup crashed for TreProject {ProjectId}", project.Id);
            }
        }

        await dbContext.SaveChangesAsync();
    }
}
