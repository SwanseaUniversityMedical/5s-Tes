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

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = new List<IDashboardAuthorizationFilter>()
    {
        new BasicAuthAuthorizationFilter(new BasicAuthAuthorizationFilterOptions
        {
            RequireSsl = true,
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
