using FiveSafesTes.Core.Models;
using FiveSafesTes.Core.Services;
using Serilog;
using Submission.Api.Repositories.DbContexts;
using Submission.Api.Services.Contract;

namespace Submission.Api.Services;

public interface ITreProvisioningService
{
    Task StoreTreWithServiceAccountAsync(Tre tre, bool isNew);
}

public class TreProvisioningService : ITreProvisioningService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IKeycloakAdminService _keycloakAdminService;
    private readonly IVaultCredentialsService _vaultCredentialsService;

    public TreProvisioningService(ApplicationDbContext dbContext, IKeycloakAdminService keycloakAdminService, IVaultCredentialsService vaultCredentialsService)
    {
        _dbContext = dbContext;
        _keycloakAdminService = keycloakAdminService;
        _vaultCredentialsService = vaultCredentialsService;
    }

    /// <summary>
    /// Saves a TRE to the database then provisions its Keycloak service account and Vault credentials.
    /// </summary>
    /// <param name="tre">The TRE we are adding or updating.</param>
    /// <param name="isNew">True if the TRE doesn't already exist in the database.</param>
    public async Task StoreTreWithServiceAccountAsync(Tre tre, bool isNew)
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

                // Write to Vault first, then persist KeycloakClientId only if the secret was stored.
                // This keeps the DB and Vault in sync: DownloadConfig needs both the client id and
                // the Vault-held secret, so we never record an id whose secret is missing from Vault.
                bool vaultWritten = await _vaultCredentialsService.AddCredentialAsync($"tre/{tre.Name.ToLowerInvariant()}/keycloak", new()
                {
                    { "clientId", clientId },
                    { "clientSecret", clientSecret },
                    { "clientUuid", clientUuid }
                });

                if (!vaultWritten)
                {
                    Log.Error("{Function} Keycloak client {ClientId} was provisioned but the Vault write failed for TRE {TreName}. KeycloakClientId was not saved and the service account needs manual setup.",
                        "TreProvisioningService", clientId, tre.Name);
                }
                else
                {
                    tre.KeycloakClientId = clientId;
                    await _dbContext.SaveChangesAsync();

                    Log.Information("{Function} Service account {ClientId} created and stored in Vault for TRE {TreName}.",
                        "TreProvisioningService", clientId, tre.Name);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "{Function} Failed to create service account for TRE {TreName}. TRE was saved but service account needs manual setup.",
                    "TreProvisioningService", tre.Name);
            }
        }
    }
}
