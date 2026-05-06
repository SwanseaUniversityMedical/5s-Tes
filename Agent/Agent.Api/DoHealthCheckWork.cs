using System.Net;
using Agent.Api.Models;
using Agent.Api.Repositories.DbContexts;
using FiveSafesTes.Core.Models.Enums;
using Hangfire;

namespace Agent.Api;

public interface IDoHealthCheckWork
{
    Task Execute();
}

public class DoHealthCheckWork : IDoHealthCheckWork
{
    private readonly ApplicationDbContext _dbContext;
    private readonly AgentSettings _agentSettings;
    private readonly JobSettings _jobSettings;

    private readonly string submissionEndpoint;

    public DoHealthCheckWork(ApplicationDbContext dbContext, IConfiguration config, AgentSettings agentSettings, JobSettings jobSettings)
    {
        _dbContext = dbContext;
        _agentSettings = agentSettings;
        _jobSettings = jobSettings;

        submissionEndpoint = config["DareAPISettings:Address"];
    }

    public async Task Execute()
    {
        DoSyncHealthCheck();
        DoAgentHealthCheck();
    }

    /// <summary>
    /// Try to connect to the submission layer and log any errors in the database.
    /// </summary>
    private bool DoSyncHealthCheck()
    {
        string message = "";
        bool isHealthy = true;

        if (string.IsNullOrEmpty(submissionEndpoint))
        {
            message = "URL for Submission API is missing.";
            isHealthy = false;
        }
        else
        {
            try
            {
                using HttpClient client = new();
                HttpResponseMessage response = client.GetAsync(submissionEndpoint + "/v1/get_test_tes").Result;

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

        Status healthStatus = new()
        {
            Product = "Submission",
            HealthStatus = isHealthy ? HealthStatus.Succeed : HealthStatus.Failed,
            Reason = message,
            DateTime = DateTime.UtcNow
        };

        _dbContext.Status.Add(healthStatus);

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

        Status healthStatus = new()
        {
            Product = "TES Engine",
            HealthStatus = isHealthy ? HealthStatus.Succeed : HealthStatus.Failed,
            Reason = message,
            DateTime = DateTime.UtcNow
        };

        _dbContext.Status.Add(healthStatus);

        if (!isHealthy) KillHangfireJobs();
        return isHealthy;
    }

    private void KillHangfireJobs()
    {
        RecurringJob.RemoveIfExists(_jobSettings.SyncJobName);
        RecurringJob.RemoveIfExists(_jobSettings.ScanJobName);
    }
}
