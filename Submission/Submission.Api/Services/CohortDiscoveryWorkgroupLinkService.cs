using FiveSafesTes.Core.Models;
using FiveSafesTes.Core.Services;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Submission.Api.Repositories.DbContexts;

namespace Submission.Api.Services;

public class CohortDiscoveryWorkgroupLinkService : BackgroundService
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private static readonly TimeSpan _pollInterval = TimeSpan.FromMinutes(1);

    public CohortDiscoveryWorkgroupLinkService(IServiceScopeFactory serviceScopeFactory)
    {
        _serviceScopeFactory = serviceScopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using IServiceScope scope = _serviceScopeFactory.CreateScope();
                ApplicationDbContext dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                ICohortDiscoverySyncService syncService = scope.ServiceProvider.GetRequiredService<ICohortDiscoverySyncService>();

                // Try linking every project user to their Cohort Discovery workgroup
                foreach (Project project in dbContext.Projects.Include(p => p.Users))
                {
                    foreach (User user in project.Users)
                    {
                        if (!string.IsNullOrWhiteSpace(user.Email))
                        {
                            // If the user has logged into the cohort discovery tool before, they have an account we can link
                            await syncService.TryLinkUserToProjectWorkgroupAsync(user.Email, project.Name);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "{Function} Failed during periodic Workgroup-link retry.", "CohortDiscoveryWorkgroupLinkService");
            }

            await Task.Delay(_pollInterval, stoppingToken);
        }
    }
}
