using System.Text.Json;
using Agent.Api.Repositories.DbContexts;
using FiveSafesTes.Core.Models;
using FiveSafesTes.Core.Models.APISimpleTypeReturns;

namespace Agent.Api.Services;

public class DmnEnvironmentVariableSyncService : IDmnEnvironmentVariableSyncService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IDareClientWithoutTokenHelper _clientHelper;
    private readonly IDmnService _dmnService;

    public DmnEnvironmentVariableSyncService(IDareClientWithoutTokenHelper clientHelper, IDmnService dmnService, ApplicationDbContext dbContext)
    {
        _clientHelper = clientHelper;
        _dmnService = dmnService;
        _dbContext = dbContext;
    }

    public async Task SyncEnvironmentVariablesWithSubmission(string dmnPath)
    {
        DmnDecisionTable dmnTable = await _dmnService.LoadDmnTableAsync(dmnPath);
        int projectColumnIndex = dmnTable.Inputs.FindIndex(x => x.Expression == "project");
        int envColumnIndex = dmnTable.Outputs.FindIndex(x => x.Name == "env");

        List<ProjectTreEnvironmentVariable> globalVariables = new();
        Dictionary<string, List<ProjectTreEnvironmentVariable>> projectVariables = new(StringComparer.OrdinalIgnoreCase);

        foreach (DmnRule rule in dmnTable.Rules) 
        {
            string? name = rule.OutputEntries[envColumnIndex].Text?.Trim('"');
            if (string.IsNullOrEmpty(name)) continue;

            ProjectTreEnvironmentVariable envVariable = new()
            {
                Name = name,
                Description = rule.Description
            };

            string? projectScope = rule.InputEntries[projectColumnIndex].Text?.Trim('"');

            if (string.IsNullOrEmpty(projectScope) || projectScope == "-")
            {
                globalVariables.Add(envVariable);
            }
            else 
            {
                if (!projectVariables.TryGetValue(projectScope, out List<ProjectTreEnvironmentVariable>? environmentVariables)) 
                {
                    environmentVariables = new();
                    projectVariables[projectScope] = environmentVariables;
                }

                environmentVariables.Add(envVariable);
            }
        }

        foreach (TreProject project in _dbContext.Projects)
        {
            List<ProjectTreEnvironmentVariable> combinedVariables = new(globalVariables);
            if (projectVariables.TryGetValue(project.SubmissionProjectName, out List<ProjectTreEnvironmentVariable>? scopedVariables))
            {
                combinedVariables.AddRange(scopedVariables);
            }

            ProjectTreEnvironmentVariables payload = new()
            {
                ProjectId = project.SubmissionProjectId,
                EnvJson = JsonSerializer.Serialize(combinedVariables, new JsonSerializerOptions { WriteIndented = true })
            };

            await _clientHelper.CallAPI<ProjectTreEnvironmentVariables, BoolReturn>("/api/Project/SyncEnvironmentVariables", payload);
        }
    }
}
