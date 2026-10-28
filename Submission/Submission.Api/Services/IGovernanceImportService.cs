using FiveSafesTes.Core.Models;
using FiveSafesTes.Core.Services;
using Serilog;
using Submission.Api.Repositories.DbContexts;
using Submission.Api.Services.Contract;

namespace Submission.Api.Services;

public interface IGovernanceImportService
{
    Task<List<string>> ApplyConfigAsync(DirectorGovernanceConfig config, Func<Tre, bool, Task>? onTreSaved = null);
}

public class GovernanceImportService : IGovernanceImportService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IKeycloakAdminService _keycloakAdminService;
    private readonly IVaultCredentialsService _vaultCredentialsService;

    public GovernanceImportService(ApplicationDbContext dbContext, IKeycloakAdminService keycloakAdminService, IVaultCredentialsService vaultCredentialsService)
    {
        _dbContext = dbContext;
        _keycloakAdminService = keycloakAdminService;
        _vaultCredentialsService = vaultCredentialsService;
    }

    public async Task<List<string>> ApplyConfigAsync(DirectorGovernanceConfig config, Func<Tre, bool, Task>? onTreSaved = null)
    {
        List<string> results = new();

        List<GovernanceProduct> fiveSafesTesProjects = (config.EnabledProducts ?? new())
            .Where(x => x.ProductMetadata?.Name?.StartsWith("5SAFETES", StringComparison.OrdinalIgnoreCase) == true).ToList();

        if (fiveSafesTesProjects.Count == 0)
        {
            results.Add("5S-TES is not an enabled product in this configuration. No TREs created.");
            return results;
        }

        List<int> treIdsToCreate = fiveSafesTesProjects.SelectMany(x => x.AssignedTres ?? new List<int>()).Distinct().ToList();

        foreach (int treId in treIdsToCreate)
        {
            GovernanceTre? configTre = config.ConsortiumTres?.FirstOrDefault(x => x.Id == treId);
            if (configTre == null)
            {
                results.Add($"TRE id {treId} is assigned to a 5S-TES product but not found in consortiumTres. Skipped.");
                continue;
            }

            GovernanceUser? adminUser = config.PresentUsers?.FirstOrDefault(x => x.RepresentedTreId == treId
                && string.Equals(x.Role, "admin", StringComparison.OrdinalIgnoreCase));

            if (adminUser == null)
            {
                results.Add($"TRE '{configTre.Name}' has no user with role 'admin' for this TRE. Skipped.");
                continue;
            }

            Tre? existing = _dbContext.Tres.FirstOrDefault(x => x.Name.ToLower() == configTre.Name.ToLower().Trim());
            bool isNew = existing == null;
            Tre tre = existing ?? new Tre { Name = configTre.Name.Trim(), FormData = string.Empty };
            tre.AdminUsername = adminUser.Email;
            tre.About = configTre.OtherInformation;

            await StoreTreWithServiceAccountAsync(tre, isNew);

            string message = $"TRE '{tre.Name}' {(isNew ? "created" : "updated")}, admin set to {tre.AdminUsername}.";
            Log.Information("{Function} {Message}", "GovernanceImportService", message);
            results.Add(message);

            if (onTreSaved != null)
            {
                await onTreSaved(tre, isNew);
            }
        }

        return results;
    }

    private async Task StoreTreWithServiceAccountAsync(Tre tre, bool isNew)
    {
        if (isNew)
        {
            _dbContext.Tres.Add(tre);
        }
        else 
        {
            _dbContext.Tres.Update(tre);
        }

        await _dbContext.SaveChangesAsync();

        if (string.IsNullOrEmpty(tre.KeycloakClientId))
        {
            try
            {
                (string? clientUuid, string? clientId, string? clientSecret) = await _keycloakAdminService.CreateServiceAccountAsync(tre.Name);

                bool vaultWritten = await _vaultCredentialsService.AddCredentialAsync($"tre/{tre.Name.ToLowerInvariant()}/keycloak", new()
                        {
                            { "clientId", clientId },
                            { "clientSecret", clientSecret },
                            { "clientUuid", clientUuid }
                        });

                if (!vaultWritten) 
                {
                    Log.Error("{Function} Keycloak client {ClientId} was provisioned but the Vault write failed for TRE {TreName}.", "GovernanceImportService", clientId, tre.Name);
                }
                else
                {
                    tre.KeycloakClientId = clientId;
                    await _dbContext.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "{Function} Failed to create service account for TRE {TreName}.","GovernanceImportService", tre.Name);
            }
        }
    }
}
