using FiveSafesTes.Core.Models;

namespace FiveSafesTes.Core.Services;

public interface ICohortDiscoverySyncService
{
    Task SyncTre(Tre tre, bool isNew = false);
    Task SyncProject(Project project, List<Tre> tres, bool isNew);
    Task<bool> TryLinkUserToProjectWorkgroupAsync(string email, string projectName);
}
