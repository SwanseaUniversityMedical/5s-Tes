using FiveSafesTes.Core.Models;
using FiveSafesTes.Core.Services;
using Serilog;
using Submission.Api.Repositories.DbContexts;
using Submission.Api.Services.Contract;

namespace Submission.Api.Services;

public interface IGovernanceImportService
{
    Task<List<string>> ApplyConfigAsync(DirectorGovernanceConfig config, Func<Tre, bool, Task>? onTreSaved = null, Func<Project, List<Tre>, bool, Task>? onProjectSaved = null);
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

    /// <summary>
    /// Creates and sets up the TREs defined in a Director Governance JSON config file.
    /// </summary>
    /// <param name="config">The Deserialized Governance config.</param>
    /// <param name="onTreSaved">The function that should be invoked when a TRE is saved.</param>
    /// <param name="onProjectSaved">The function that should be invoked when a project is saved.</param>
    /// <returns>A list containing a result message that describes the outcome for each TRE processed.</returns>
    public async Task<List<string>> ApplyConfigAsync(DirectorGovernanceConfig config, Func<Tre, bool, Task>? onTreSaved = null, Func<Project, List<Tre>, bool, Task>? onProjectSaved = null)
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
        List<Tre> importedTres = new();

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

            try
            {
                await _keycloakAdminService.SetTreAdminCustodianAttributeAsync(tre.AdminUsername, tre.Name);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "{Function} Failed to set custodian attribute for TRE admin {AdminUsername}.", "GovernanceImportService", tre.AdminUsername);
            }

            importedTres.Add(tre);

            string message = $"TRE '{tre.Name}' {(isNew ? "created" : "updated")}, admin set to {tre.AdminUsername}.";
            Log.Information("{Function} {Message}", "GovernanceImportService", message);
            results.Add(message);

            if (onTreSaved != null)
            {
                await onTreSaved(tre, isNew);
            }
        }

        if (importedTres.Count > 0 && !string.IsNullOrWhiteSpace(config.Name)) 
        {
            await UpsertProjectAsync(config, importedTres, results, onProjectSaved);
        }

        return results;
    }

    /// <summary>
    /// Saves a TRE to the database.
    /// Provisions a Keycloak service account and Vault credential to TREs that do not already have one.
    /// </summary>
    /// <param name="tre">The TRE we are adding or updating.</param>
    /// <param name="isNew">True if the TRE doesn't already exist in the database.</param>
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

    /// <summary>
    /// Creates or updates a project representing a given consortium, linking its TREs respectively.
    /// </summary>
    /// <param name="config">The imported configuration from Director Governance.</param>
    /// <param name="tres">The list of TREs that have been created from the Director Governance config.</param>
    /// <param name="results">A list containing a result message describing the outcome for each TRE processed.</param>
    private async Task UpsertProjectAsync(DirectorGovernanceConfig config, List<Tre> tres, List<string> results, Func<Project, List<Tre>, bool, Task>? onProjectSaved)
    {
        Project? existing = _dbContext.Projects.FirstOrDefault(x => x.Name.ToLower() == config.Name.Trim().ToLower());
        bool isNew = existing == null;

        Project project = existing ?? new()
        {
            Name = config.Name.Trim(),
            Display = config.Name.Trim(),
            StartDate = DateTime.UtcNow,
            EndDate = DateTime.UtcNow.AddYears(3), // TODO check how we choose end date
            SubmissionBucket = "",
            OutputBucket = "",
            FormData = string.Empty,
            Tres = [],
            Users = [],
            Submissions = []
        };

        project.ProjectDescription = config.Description ?? project.ProjectDescription ?? "";

        if (isNew) _dbContext.Projects.Add(project);

        foreach (Tre tre in tres) 
        {
            if (!project.Tres.Any(t => t.Id == tre.Id))
            {
                project.Tres.Add(tre);
            }
        }

        await _dbContext.SaveChangesAsync();

        string message = $"Project '{project.Name}' {(isNew ? "created" : "updated")}, linked to {tres.Count} TRE(s).";
        Log.Information("{Function} {Message}", "GovernanceImportService", message);
        results.Add(message);

        if (onProjectSaved != null) await onProjectSaved(project, tres, isNew);
    }
}
