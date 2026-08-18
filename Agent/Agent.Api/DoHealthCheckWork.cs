using System.Net;
using Agent.Api.Helpers;
using Agent.Api.Models;
using Agent.Api.Repositories.DbContexts;
using FiveSafesTes.Core.Models.Enums;
using FiveSafesTes.Core.Models.Settings;
using FiveSafesTes.Core.Rabbit;
using FiveSafesTes.Core.Services;
using Hangfire;
using Microsoft.Extensions.Options;
using FiveSafesTes.Core.Models;
using Microsoft.EntityFrameworkCore;
using RabbitMQ.Client;

namespace Agent.Api;

public interface IDoHealthCheckWork
{
  [AutomaticRetry(Attempts = 0)]
  Task Execute();
}

public class DoHealthCheckWork(
  ApplicationDbContext dbContext,
  AgentSettings agentSettings,
  JobSettings jobSettings,
  IOptionsMonitor<DataEgressKeyCloakSettings> egressKeycloakSettings,
  IEncDecHelper encDecHelper,
  IOptions<ApiEndpointSettings> apiEndpointSettings,
  IConfiguration configuration,
  RabbitMQSetting rabbitSettings)
  : IDoHealthCheckWork
{
  private readonly ApiEndpointSettings _apiEndpoints = apiEndpointSettings.Value;

  public async Task Execute()
  {
    await DeleteOldLogs();
    await DoSyncHealthCheck();
    await DoAgentHealthCheck();
    DoRabbitMqHealthCheck();
    await DoEgressHealthCheck();
    await dbContext.SaveChangesAsync();
  }

  /// <summary>
  /// Try to connect to the submission layer and log any errors in the database.
  /// </summary>
  private async Task<bool> DoSyncHealthCheck()
  {
    string message = "";
    bool isHealthy = true;

    if (string.IsNullOrEmpty(_apiEndpoints.SubmissionApiUrl))
    {
      message = "URL for Submission API is missing.";
      isHealthy = false;
    }
    else
    {
      try
      {
        using HttpClient client = new();
        HttpResponseMessage response =
          await client.GetAsync(UrlHelper.Combine(_apiEndpoints.SubmissionApiUrl, "api/HealthCheck/CheckHealth"));

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
      HealthStatus = isHealthy ? HealthStatus.Connected : HealthStatus.Failed,
      Reason = message,
      DateTime = DateTime.UtcNow
    };

    dbContext.HealthCheckStatus.Add(healthStatus);

    if (!isHealthy) KillHangfireJobs();
    return isHealthy;
  }

  /// <summary>
  /// Try to connect to TESK and log any errors in the database.
  /// </summary>
  private async Task<bool> DoAgentHealthCheck()
  {
    string message = "";
    bool isHealthy = true;

    if (string.IsNullOrEmpty(agentSettings.TESKAPIURL))
    {
      isHealthy = false;
      message = "TES API URL is missing.";
    }
    else
    {
      try
      {
        HttpClientHandler handler = new();

        if (agentSettings.Proxy)
        {
          handler = new HttpClientHandler
          {
            Proxy = new WebProxy(agentSettings.ProxyAddresURL, true),
            UseProxy = agentSettings.Proxy,
          };
        }

        using HttpClient client = new(handler);
        HttpResponseMessage response = await client.GetAsync(agentSettings.TESKAPIURL);

        if (!response.IsSuccessStatusCode)
        {
          isHealthy = false;
          message = "Failed to reach TES API.";
        }
      }
      catch (Exception)
      {
        isHealthy = false;
        message = "Failed to reach TES API.";
      }
    }

    // Log health status for TES engine in the database.
    HealthCheckStatus healthStatus = new()
    {
      Product = "TES Engine",
      HealthStatus = isHealthy ? HealthStatus.Connected : HealthStatus.Failed,
      Reason = message,
      DateTime = DateTime.UtcNow
    };

    dbContext.HealthCheckStatus.Add(healthStatus);

    if (!isHealthy) KillHangfireJobs();
    return isHealthy;
  }

  /// <summary>
  /// Check that the Agent can reach the RabbitMQ broker and log the result.
  /// If the broker is unreachable the Agent can't receive tasks from the Submission queue, so
  /// we also stop the sync/scan jobs — that way the Submission side sees the TRE as offline and
  /// won't queue work that cannot run.
  /// </summary>
  private void DoRabbitMqHealthCheck()
  {
    bool isHealthy = false;
    string message = "";

    try
    {
      var factory = new ConnectionFactory
      {
        HostName = rabbitSettings.HostAddress,
        Port = int.Parse(rabbitSettings.PortNumber),
        VirtualHost = rabbitSettings.VirtualHost,
        UserName = rabbitSettings.Username,
        Password = rabbitSettings.Password,
        RequestedConnectionTimeout = TimeSpan.FromSeconds(5)
      };

      using IConnection connection = factory.CreateConnection();
      isHealthy = connection.IsOpen;

      if (!isHealthy)
      {
        message = "Not connected to RabbitMQ broker.";
      }
    }
    catch (Exception)
    {
      isHealthy = false;
      message = "Failed to reach RabbitMQ broker.";
    }

    // Log health status for the RabbitMQ broker in the database.
    HealthCheckStatus healthStatus = new()
    {
      Product = "RabbitMQ",
      HealthStatus = isHealthy ? HealthStatus.Connected : HealthStatus.Failed,
      Reason = message,
      DateTime = DateTime.UtcNow
    };

    dbContext.HealthCheckStatus.Add(healthStatus);

    if (!isHealthy) KillHangfireJobs();
  }

  /// <summary>
  /// If a connection is unhealthy, we stop our hangfire jobs so that they are not trying to hit unreachable endpoints repeatedly.
  /// </summary>
  private void KillHangfireJobs()
  {
    RecurringJob.RemoveIfExists(jobSettings.SyncJobName);
    RecurringJob.RemoveIfExists(jobSettings.ScanJobName);
  }

  /// <summary>
  /// Checks that our data egress credentials are valid.
  /// </summary>
  private async Task DoEgressHealthCheck()
  {
    DataEgressKeyCloakSettings keycloakSettings = egressKeycloakSettings.CurrentValue;

    bool isHealthy = false;
    string message = "";

    if (!string.IsNullOrEmpty(keycloakSettings.Username) && !string.IsNullOrEmpty(keycloakSettings.PasswordEnc))
    {
      var keycloakDemoMode =
        KeycloakCommon.ResolveKeycloakDemoMode(keycloakSettings.KeycloakDemoMode, configuration["KeycloakDemoMode"]);
      KeycloakTokenHelper keycloakTokenHelper = new(keycloakSettings.BaseUrl, keycloakSettings.ClientId,
        keycloakSettings.ClientSecret, keycloakSettings.Proxy, keycloakSettings.ProxyAddresURL, keycloakDemoMode);

      // Attempt to connect to egress using current credentials
      var token = await keycloakTokenHelper.GetTokenForUser(keycloakSettings.Username,
        encDecHelper.Decrypt(keycloakSettings.PasswordEnc), "dare-tre-admin");
      isHealthy = !string.IsNullOrWhiteSpace(token.token);

      if (!isHealthy)
      {
        message = "Invalid Egress Credentials.";
      }
    }
    else
    {
      message = "Missing Egress Credentials.";
    }

    // Log health status for egress connection
    HealthCheckStatus healthStatus = new()
    {
      Product = "Egress",
      HealthStatus = isHealthy ? HealthStatus.Connected : HealthStatus.Failed,
      Reason = message,
      DateTime = DateTime.UtcNow
    };

    dbContext.HealthCheckStatus.Add(healthStatus);
  }

    /// <summary>
    /// Logs that are more than 30 days old are removed from the database.
    /// </summary>
    private async Task DeleteOldLogs()
    {
        DateTime cutoffDate = DateTime.UtcNow.AddDays(-jobSettings.DaysBeforeHealthLogDeletion);

        await dbContext.HealthCheckStatus.Where(x => x.DateTime < cutoffDate).ExecuteDeleteAsync();

    }
}
