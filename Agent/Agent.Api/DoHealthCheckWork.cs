using System.Net;
using Agent.Api.Models;
using Agent.Api.Repositories.DbContexts;
using FiveSafesTes.Core.Models.Enums;
using FiveSafesTes.Core.Models.Settings;
using FiveSafesTes.Core.Services;
using Hangfire;
using Microsoft.Extensions.Options;
using FiveSafesTes.Core.Models;

namespace Agent.Api;

public interface IDoHealthCheckWork
{
    [AutomaticRetry(Attempts = 0)]
    Task Execute();
}

public class DoHealthCheckWork : IDoHealthCheckWork
{
    private readonly ApplicationDbContext _dbContext;
    private readonly AgentSettings _agentSettings;
    private readonly JobSettings _jobSettings;
    private readonly IOptionsMonitor<DataEgressKeyCloakSettings> _egressKeycloakSettings;
    private readonly IEncDecHelper _encDecHelper;

    private readonly string _submissionEndpoint;
    private readonly IConfiguration _configuration;

    public DoHealthCheckWork(ApplicationDbContext dbContext, IConfiguration config, AgentSettings agentSettings, JobSettings jobSettings, 
        IOptionsMonitor<DataEgressKeyCloakSettings> egressKeycloakSettings, IEncDecHelper encDecHelper)
    {
        _dbContext = dbContext;
        _agentSettings = agentSettings;
        _jobSettings = jobSettings;
        _egressKeycloakSettings = egressKeycloakSettings;
        _configuration = config;

        _submissionEndpoint = config["DareAPISettings:Address"];
        _encDecHelper = encDecHelper;
    }

    public async Task Execute()
    {
        DoSyncHealthCheck();
        DoAgentHealthCheck();
        await DoEgressHealthCheck();

        _dbContext.SaveChanges();
    }

    /// <summary>
    /// Try to connect to the submission layer and log any errors in the database.
    /// </summary>
    private bool DoSyncHealthCheck()
    {
        string message = "";
        bool isHealthy = true;

        if (string.IsNullOrEmpty(_submissionEndpoint))
        {
            message = "URL for Submission API is missing.";
            isHealthy = false;
        }
        else
        {
            try
            {
                using HttpClient client = new();
                HttpResponseMessage response = client.GetAsync(_submissionEndpoint + "/v1/get_test_tes").Result;

                if (!response.IsSuccessStatusCode)
                {
                    isHealthy = false;
                    message = "Failed to reach Submission API.";
                }
            }
            catch (Exception)
            {
                isHealthy = false;
                message = "Invalid URL for Submission API.";
            }
        }

        // Log health status for submission layer in the database.
        HealthCheckStatus healthStatus = new()
        {
            Product = "Submission",
            HealthStatus = isHealthy ? HealthStatus.Succeed : HealthStatus.Failed,
            Reason = message,
            DateTime = DateTime.UtcNow
        };

        _dbContext.HealthCheckStatus.Add(healthStatus);

        if (!isHealthy) KillHangfireJobs();
        return isHealthy;
    }

    /// <summary>
    /// Try to connect to TESK and log any errors in the database.
    /// </summary>
    private bool DoAgentHealthCheck()
    {
        string message = "";
        bool isHealthy = true;

        if (string.IsNullOrEmpty(_agentSettings.TESKAPIURL))
        {
            isHealthy = false;
            message = "TESK API URL is missing.";
        }
        else
        {
            try
            {
                HttpClientHandler handler = new();

                if (_agentSettings.Proxy)
                {
                    handler = new HttpClientHandler
                    {
                        Proxy = new WebProxy(_agentSettings.ProxyAddresURL, true),
                        UseProxy = _agentSettings.Proxy,
                    };
                }

                using HttpClient client = new(handler);
                HttpResponseMessage response = client.GetAsync(_agentSettings.TESKAPIURL).Result;

                if (!response.IsSuccessStatusCode)
                {
                    isHealthy = false;
                    message = "Failed to reach TESK API.";
                }
            }
            catch (Exception)
            {
                isHealthy = false;
                message = "Invalid URL for TESK API.";
            }
        }

        // Log health status for TES engine in the database.
        HealthCheckStatus healthStatus = new()
        {
            Product = "TES Engine",
            HealthStatus = isHealthy ? HealthStatus.Succeed : HealthStatus.Failed,
            Reason = message,
            DateTime = DateTime.UtcNow
        };

        _dbContext.HealthCheckStatus.Add(healthStatus);

        if (!isHealthy) KillHangfireJobs();
        return isHealthy;
    }

    /// <summary>
    /// If a connection is unhealthy, we stop our hangfire jobs so that they are not trying to hit unreachable endpoints repeatedly.
    /// </summary>
    private void KillHangfireJobs()
    {
        RecurringJob.RemoveIfExists(_jobSettings.SyncJobName);
        RecurringJob.RemoveIfExists(_jobSettings.ScanJobName);
    }

    /// <summary>
    /// Checks that our data egress credentials are valid.
    /// </summary>
    private async Task DoEgressHealthCheck()
    {
        DataEgressKeyCloakSettings keycloakSettings = _egressKeycloakSettings.CurrentValue;

        bool isHealthy = false;
        string message = "";

        if (!string.IsNullOrEmpty(keycloakSettings.Username) && !string.IsNullOrEmpty(keycloakSettings.PasswordEnc))
        {
            var keycloakDemoMode = KeycloakCommon.ResolveKeycloakDemoMode(keycloakSettings.KeycloakDemoMode, _configuration["KeycloakDemoMode"]);
            KeycloakTokenHelper keycloakTokenHelper = new(keycloakSettings.BaseUrl, keycloakSettings.ClientId,
                    keycloakSettings.ClientSecret, keycloakSettings.Proxy, keycloakSettings.ProxyAddresURL, keycloakDemoMode);

            // Attempt to connect to egress using current credentials
            var token = await keycloakTokenHelper.GetTokenForUser(keycloakSettings.Username, _encDecHelper.Decrypt(keycloakSettings.PasswordEnc), "dare-tre-admin");
            isHealthy = !string.IsNullOrWhiteSpace(token.token);
        }
        else
        {
            message = "Missing Egress Credentials.";
        }

        // Log health status for egress connection
        HealthCheckStatus healthStatus = new()
        {
            Product = "Egress",
            HealthStatus = isHealthy ? HealthStatus.Succeed : HealthStatus.Failed,
            Reason = message,
            DateTime = DateTime.UtcNow
        };

        _dbContext.HealthCheckStatus.Add(healthStatus);
    }
}
