using System;
using System.Collections;
using System.Collections.Generic;
using System.Net.Http.Headers;
using System.Text;
using FiveSafesTes.Core.Models;
using FiveSafesTes.Core.Models.Settings;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Serilog;

namespace FiveSafesTes.Core.Services;

public class CohortDiscoverySyncService : ICohortDiscoverySyncService
{
    private readonly HttpClient _httpClient;
    private readonly CohortDiscoverySettings _settings;
    private string? _accessToken;

    private const string AUTH_ENDPOINT = "api/auth/login";
    private const string CUSTODIAN_ENDPOINT = "api/v1/custodians";
    private const string COLLECTION_HOST_ENDPOINT = "api/v1/collection_hosts";
    private const string COLLECTION_ENDPOINT = "api/v1/collections";
    private const string WORKGROUP_ENDPOINT = "api/v1/workgroups";
    private const string USER_ENDPOINT = "api/v1/users";

    public CohortDiscoverySyncService(HttpClient httpClient, IOptions<CohortDiscoverySettings> options)
    {
        _httpClient = httpClient;
        _settings = options.Value;
    }

    /// <summary>
    /// Ensures that a TRE has a matching Custodian, CollectionHost and Collection in the Cohort Discovery Tool, 
    /// creating any that do not exist already.
    /// </summary>
    /// <param name="tre">The TRE that we are syncing with the Cohort Discovery Tool.</param>
    /// <param name="isNew">Unused but required by the call in ConsumeDirectorGovernanceService.</param>
    public async Task SyncTre(Tre tre, bool isNew = false)
    {
        if (!_settings.EnableCohortDiscoveryTool)
        {
            Log.Debug("{Function} Cohort Discovery sync is disabled, skipping TRE '{TreName}'.",  "CohortDiscoverySyncService", tre.Name);
            return;
        }

        try
        {
            await EnsureAuthentication();

            CustodianDto custodian = await FindOrCreateCustodian(tre.Name);
            CollectionHostDto collectionHost = await FindOrCreateCollectionHost(custodian);
            await FindOrCreateCollection(custodian, collectionHost, tre);

            Log.Information("{Function} Synced TRE '{TreName}' to Cohort Discovery (custodian id {CustodianId}).", "CohortDiscoverySyncService", tre.Name, custodian.Id);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "{Function} Failed to sync TRE '{TreName}' to Cohort Discovery.", "CohortDiscoverySyncService", tre.Name);
        }
    }

    /// <summary>
    /// Logs into to the Cohort Discovery Tool to provide us with a bearer token for our Http Client.
    /// </summary>
    private async Task EnsureAuthentication()
    {
        if (_accessToken != null) return;

        var payload = new { email = _settings.AdminEmail, password = _settings.AdminPassword };
        StringContent content = new(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");

        HttpResponseMessage response = await _httpClient.PostAsync(AUTH_ENDPOINT, content);
        response.EnsureSuccessStatusCode();

        Envelope<LoginData>? envelope = JsonConvert.DeserializeObject<Envelope<LoginData>>(await response.Content.ReadAsStringAsync());
        _accessToken = envelope?.Data?.AccessToken ?? throw new InvalidOperationException("Cohort Discovery login did not return an access token.");

        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);

    }

    /// <summary>
    /// Finds an existing Custodian matching the name of the given TRE or creates one if none exist.
    /// </summary>
    /// <param name="treName">The TRE name to match or create a Custodian for.</param>
    /// <returns>The matching or newly-created Custodian.</returns>
    private async Task<CustodianDto> FindOrCreateCustodian(string treName)
    {
        HttpResponseMessage custodianListResponse = await _httpClient.GetAsync(CUSTODIAN_ENDPOINT);
        custodianListResponse.EnsureSuccessStatusCode();

        Envelope<List<CustodianDto>>? list = JsonConvert.DeserializeObject<Envelope<List<CustodianDto>>>(await custodianListResponse.Content.ReadAsStringAsync());
        CustodianDto? existing = list?.Data?.FirstOrDefault(x => string.Equals(x.Name, treName, StringComparison.OrdinalIgnoreCase));

        if (existing != null) return existing;

        // No Custodian exists for the given TRE - create one.
        var creationPayload = new { name = treName };
        StringContent content = new(JsonConvert.SerializeObject(creationPayload), Encoding.UTF8, "application/json");

        HttpResponseMessage creationResponse = await _httpClient.PostAsync(CUSTODIAN_ENDPOINT, content);
        creationResponse.EnsureSuccessStatusCode();

        Envelope<CustodianDto>? createdCustodian = JsonConvert.DeserializeObject<Envelope<CustodianDto>>(await creationResponse.Content.ReadAsStringAsync());

        // Custodian creation responses do not contain the pid field so we must fetch the newly-created custodian to obtain it.
        HttpResponseMessage getResponse = await _httpClient.GetAsync($"{CUSTODIAN_ENDPOINT}/{createdCustodian!.Data!.Id}");
        getResponse.EnsureSuccessStatusCode();

        Envelope<CustodianDto>? fetched = JsonConvert.DeserializeObject<Envelope<CustodianDto>>(await getResponse.Content.ReadAsStringAsync());
        return fetched!.Data!;
    }

    /// <summary>
    /// Finds an existing CollectionHost under a given Custodian or creates one if none exist.
    /// </summary>
    /// <param name="custodian">The Custodian we want to create the CollectionHost under.</param>
    /// <returns>The matching or newly-created CollectionHost.</returns>
    private async Task<CollectionHostDto> FindOrCreateCollectionHost(CustodianDto custodian)
    {
        HttpResponseMessage hostListResponse = await _httpClient.GetAsync($"{CUSTODIAN_ENDPOINT}/{custodian.Pid}/collection_hosts");
        hostListResponse.EnsureSuccessStatusCode();

        Envelope<List<CollectionHostDto>>? list = JsonConvert.DeserializeObject<Envelope<List<CollectionHostDto>>>(await hostListResponse.Content.ReadAsStringAsync());
        CollectionHostDto? existing = list?.Data?.FirstOrDefault();

        if (existing != null) return existing;

        // No CollectionHost exists for the given Custodian - create one.
        var creationPayload = new
        {
            name = $"{custodian.Name} host",
            query_context_type = _settings.DefaultQueryContextType,
            custodian_id = custodian.Id
        };

        StringContent content = new (JsonConvert.SerializeObject(creationPayload), Encoding.UTF8, "application/json");
        HttpResponseMessage response = await _httpClient.PostAsync(COLLECTION_HOST_ENDPOINT, content);
        response.EnsureSuccessStatusCode();

        Envelope<CollectionHostDto>? createdHost = JsonConvert.DeserializeObject<Envelope<CollectionHostDto>>(await response.Content.ReadAsStringAsync());
        return createdHost!.Data!;
    }

    /// <summary>
    /// Finds an existing Collection matching the name of the given TRE under the given Custodian or creates one if none exist.
    /// </summary>
    /// <param name="custodian">The Custodian the Collection belongs to.</param>
    /// <param name="host">The CollectionHost the Collection should use.</param>
    /// <param name="tre">The TRE represented by the Collection.</param>
    private async Task FindOrCreateCollection(CustodianDto custodian, CollectionHostDto host, Tre tre)
    {
        HttpResponseMessage collectionListResponse = await _httpClient.GetAsync($"{CUSTODIAN_ENDPOINT}/{custodian.Pid}/collections");
        collectionListResponse.EnsureSuccessStatusCode();

        var list = JsonConvert.DeserializeObject<PaginatedEnvelope<List<CollectionDto>>>(await collectionListResponse.Content.ReadAsStringAsync());

        bool existing = list?.Data?.Data?.Any(x => string.Equals(x.Name, tre.Name, StringComparison.OrdinalIgnoreCase)) == true;

        if (existing) return;

        // No Collection exists for the given Custodian, CollectionHost and TRE - create one.
        var creationPayload = new
        {
            name = tre.Name,
            description = tre.About ?? $"Dataset for {tre.Name}",
            type = _settings.DefaultQueryContextType,
            host_id = host.Id
        };

        StringContent content = new StringContent(JsonConvert.SerializeObject(creationPayload), Encoding.UTF8, "application/json");
        HttpResponseMessage response = await _httpClient.PostAsync($"api/v1/custodians/{custodian.Pid}/collections", content);
        response.EnsureSuccessStatusCode();

        Envelope<CollectionDto>? createdCollection = JsonConvert.DeserializeObject<Envelope<CollectionDto>>(await response.Content.ReadAsStringAsync());

        // Newly-created Collections default to "draft" mode - send another request to activate them:
        var transitionPayload = new { state = "active" };
        StringContent transitionContent = new (JsonConvert.SerializeObject(transitionPayload), Encoding.UTF8, "application/json");
        await _httpClient.PutAsync($"{COLLECTION_ENDPOINT}/{createdCollection!.Data!.Id}/transition_to", transitionContent);
    }

    public async Task SyncProject(Project project, List<Tre> tres, bool isNew)
    {
        if (!_settings.EnableCohortDiscoveryTool)
        {
            Log.Debug("{Function} Cohort Discovery sync is disabled, skipping project '{ProjectName}'.", "CohortDiscoverySyncService", project.Name);
            return;
        }

        try
        {
            await EnsureAuthentication();

            WorkgroupDto workgroup = await FindOrCreateWorkgroup(project.Name);

            foreach (Tre tre in tres)
            {
                CollectionDto? collection = await FindCollectionByTreName(tre.Name);

                if (collection == null)
                {
                    Log.Error("{Function} No Cohort Discovery Collection found for TRE '{TreName}' - skipping workgroup link.", "CohortDiscoverySyncService", tre.Name);
                    continue;
                }

                await AddCollectionToWorkgroupAsync(collection.Id, workgroup.Id);
            }

            // TODO: add each of the project's users to the workgroup once Cohort Discovery implements user creation via the API. 

            Log.Information("{Function} Synced project '{ProjectName}' to Cohort Discovery workgroup id {WorkgroupId}.", "CohortDiscoverySyncService", project.Name, workgroup.Id);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "{Function} Failed to sync project '{ProjectName}' to Cohort Discovery.", "CohortDiscoverySyncService", project.Name);
        }
    }

    private async Task<WorkgroupDto> FindOrCreateWorkgroup(string projectName)
    {
        HttpResponseMessage workgroupListResponse = await _httpClient.GetAsync(WORKGROUP_ENDPOINT);
        workgroupListResponse.EnsureSuccessStatusCode();
        Envelope<List<WorkgroupDto>>? workgroups = JsonConvert.DeserializeObject<Envelope<List<WorkgroupDto>>>(await workgroupListResponse.Content.ReadAsStringAsync());

        WorkgroupDto? existing = workgroups?.Data?.FirstOrDefault(x => string.Equals(x.Name, projectName, StringComparison.OrdinalIgnoreCase));
        if (existing != null) return existing;

        // No workgroup already exists for the project - create one.
        var creationPayload = new { name = projectName, active = true };
        StringContent content = new (JsonConvert.SerializeObject(creationPayload), Encoding.UTF8, "application/json");
        HttpResponseMessage response = await _httpClient.PostAsync(WORKGROUP_ENDPOINT, content);
        response.EnsureSuccessStatusCode();

        Envelope<WorkgroupDto>? createdWorkgroup = JsonConvert.DeserializeObject<Envelope<WorkgroupDto>>(await response.Content.ReadAsStringAsync());
        return createdWorkgroup!.Data!;
    }

    private async Task<CollectionDto?> FindCollectionByTreName(string treName)
    {
        HttpResponseMessage custodianListResponse = await _httpClient.GetAsync(CUSTODIAN_ENDPOINT);
        custodianListResponse.EnsureSuccessStatusCode();

        Envelope<List<CustodianDto>>? custodians = JsonConvert.DeserializeObject<Envelope<List<CustodianDto>>>(await custodianListResponse.Content.ReadAsStringAsync());
        CustodianDto? custodian = custodians?.Data?.FirstOrDefault(x => string.Equals(x.Name, treName, StringComparison.OrdinalIgnoreCase));

        if (custodian == null) return null;

        HttpResponseMessage collectionListResponse = await _httpClient.GetAsync($"{CUSTODIAN_ENDPOINT}/{custodian.Pid}/collections");
        collectionListResponse.EnsureSuccessStatusCode();

        PaginatedEnvelope<List<CollectionDto>>? collections = JsonConvert.DeserializeObject<PaginatedEnvelope<List<CollectionDto>>>(await collectionListResponse.Content.ReadAsStringAsync());

        return collections?.Data?.Data?.FirstOrDefault(x => string.Equals(x.Name, treName, StringComparison.OrdinalIgnoreCase));
    }

    private async Task AddCollectionToWorkgroupAsync(int collectionId, int workgroupId)
    {
        var payload = new { workgroup_id = workgroupId };
        StringContent content = new (JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");
        HttpResponseMessage response = await _httpClient.PostAsync($"{COLLECTION_ENDPOINT}/{collectionId}/workgroup", content);
        response.EnsureSuccessStatusCode();
    }

    public async Task<bool> TryLinkUserToProjectWorkgroupAsync(string adminEmail, string projectName)
    {
        if (!_settings.EnableCohortDiscoveryTool) return false;

        try
        {
            await EnsureAuthentication();

            HttpResponseMessage userListResponse = await _httpClient.GetAsync(USER_ENDPOINT);
            userListResponse.EnsureSuccessStatusCode();
            var users = JsonConvert.DeserializeObject<Envelope<List<UserDto>>>(await userListResponse.Content.ReadAsStringAsync());
            UserDto? user = users?.Data?.FirstOrDefault(u => string.Equals(u.Email, adminEmail, StringComparison.OrdinalIgnoreCase));

            if (user == null)
            {
                // Hasn't logged in yet, try again next cycle.
                return false;
            }

            WorkgroupDto? workgroup = await FindWorkgroupByName(projectName);
            if (workgroup == null) return false;

            await AddUserToWorkgroupAsync(user.Id, workgroup.Id);

            Log.Information("{Function} Linked user {Email} to workgroup '{ProjectName}'.", "CohortDiscoverySyncService", adminEmail, projectName);
            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "{Function} Failed linking {Email} to workgroup '{ProjectName}'.", "CohortDiscoverySyncService", adminEmail, projectName);
            return false;
        }
    }

    private async Task<WorkgroupDto?> FindWorkgroupByName(string projectName)
    {
        HttpResponseMessage workgroupListResponse = await _httpClient.GetAsync(WORKGROUP_ENDPOINT);
        workgroupListResponse.EnsureSuccessStatusCode();
        Envelope<List<WorkgroupDto>>? workgroups = JsonConvert.DeserializeObject<Envelope<List<WorkgroupDto>>>(await workgroupListResponse.Content.ReadAsStringAsync());

        return workgroups?.Data?.FirstOrDefault(x => string.Equals(x.Name, projectName, StringComparison.OrdinalIgnoreCase));
    }

    private async Task AddUserToWorkgroupAsync(int userId, int workgroupId)
    {
        var payload = new { workgroup_id = workgroupId };
        StringContent content = new(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");
        HttpResponseMessage response = await _httpClient.PostAsync($"{USER_ENDPOINT}/{userId}/workgroup", content);
        response.EnsureSuccessStatusCode();
    }

    /// <summary>
    /// A generic wrapper class that matches the outer JSON shape of a Cohort Discovery API response.
    /// </summary>
    private class Envelope<T>
    {
        [JsonProperty("data")]
        public T? Data { get; set; }
    }

    private class PaginatedEnvelope<T>
    {
        [JsonProperty("data")]
        public PaginatedPage<T>? Data { get; set; }
    }

    private class PaginatedPage<T>
    {
        [JsonProperty("data")]
        public T? Data { get; set; }
    }

    private class LoginData
    {
        [JsonProperty("access_token")]
        public string? AccessToken { get; set; }
    }

    private class CustodianDto
    {
        public int Id { get; set; }
        public string Pid { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
    }

    private class CollectionHostDto
    {
        public int Id { get; set; }
    }

    private class CollectionDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    private class WorkgroupDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    private class UserDto
    {
        public int Id { get; set; }
        public string Email { get; set; } = string.Empty;
    }

}
