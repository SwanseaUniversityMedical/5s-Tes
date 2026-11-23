using FiveSafesTes.Core.Extensions;
using FiveSafesTes.Core.Models.Settings;
using FiveSafesTes.Core.Services;
using Hangfire;
using Hangfire.Dashboard;
using Hangfire.Dashboard.BasicAuthorization;
using Hangfire.PostgreSql;
using Microsoft.Extensions.Options;
using Serilog;
using TeleportUserManagement.Models.Settings;
using TeleportUserManagement.Services;

var builder = WebApplication.CreateBuilder(args);

ConfigurationManager configuration = builder.Configuration;
IWebHostEnvironment environment = builder.Environment;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .Enrich.WithProperty("ApplicationContext", environment.ApplicationName)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.Seq(configuration["Serilog:SeqServerUrl"], apiKey: configuration["Serilog:SeqApiKey"])
    .ReadFrom.Configuration(configuration)
    .CreateLogger();

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddHttpContextAccessor();
builder.Services.AddHttpClient();
builder.Services.AddHealthChecks();

builder.Services.AddScoped<ILdapService, LdapService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<ISubmissionClientHelper, SubmissionClientHelper>();

var activeDirectorySettings = new ActiveDirectorySettings();
configuration.Bind(nameof(ActiveDirectorySettings), activeDirectorySettings);
builder.Services.AddSingleton(activeDirectorySettings);

var jobSettings = new JobSettings();
configuration.Bind(nameof(JobSettings), jobSettings);
builder.Services.AddSingleton(jobSettings);

builder.Services.AddKeycloakSettings<SubmissionKeyCloakSettings>(configuration, nameof(SubmissionKeyCloakSettings));
builder.Services.Configure<ApiEndpointSettings>(configuration.GetSection("ApiEndpoints"));

builder.Services.AddSingleton(new AutomaticRetryAttribute() { Attempts = 0 });

string hangfireConnectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddHangfire((provider, config) =>
{
    config.UsePostgreSqlStorage(options => options.UseNpgsqlConnection(hangfireConnectionString));
    config.UseFilter(provider.GetRequiredService<AutomaticRetryAttribute>());
});

builder.Services.AddHangfireServer();

var app = builder.Build();

if (Environment.GetEnvironmentVariable("PUSHGATEWAY_URL") != null)
{
    var pusher = new Prometheus.MetricPusher(new Prometheus.MetricPusherOptions
    {
        Endpoint = Environment.GetEnvironmentVariable("PUSHGATEWAY_URL"),
        Job = Environment.GetEnvironmentVariable("PUSHGATEWAY_JOB")
    });
    pusher.Start();
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// Disable redirect if using http only site to prevent silent redirect to non existent https site
var httpsRedirect = configuration["httpsRedirect"];
if (httpsRedirect != null && httpsRedirect.ToLower() == "true")
{
    app.UseHttpsRedirection();
}

app.UseAuthorization();

app.MapControllers();

// Anonymous: probed by Kubernetes, which cannot authenticate.
app.MapHealthChecks("/health").AllowAnonymous();

using (var scope = app.Services.CreateScope())
{
    var vaultCredentialsService = scope.ServiceProvider.GetRequiredService<IVaultCredentialsService>();
}


app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = new List<IDashboardAuthorizationFilter>()
    {
        new BasicAuthAuthorizationFilter(new BasicAuthAuthorizationFilterOptions
        {
            // TLS terminates before the pod; requiring SSL here would lock
            // out the dashboard in-cluster.
            RequireSsl = false,
            SslRedirect = false,
            LoginCaseSensitive = false,
            Users = new[]
            {
                new BasicAuthAuthorizationUser
                {
                    Login = configuration["Hangfire:Username"],
                    PasswordClear = configuration["Hangfire:Password"],
                },
            },
        }),
    },
});

RecurringJob.AddOrUpdate<IUserService>(
    "ProjectDiscovery",
    x => x.DiscoverProjects(),
    Cron.MinuteInterval(jobSettings.ProjectDiscoverySchedule));

app.Run();
