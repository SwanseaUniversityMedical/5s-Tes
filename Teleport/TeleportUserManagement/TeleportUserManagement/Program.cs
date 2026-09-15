using FiveSafesTes.Core.Extensions;
using FiveSafesTes.Core.Models.Settings;
using FiveSafesTes.Core.Services;
using Hangfire;
using Hangfire.Dashboard;
using Hangfire.Dashboard.BasicAuthorization;
using Hangfire.PostgreSql;
using Microsoft.Extensions.Options;
using TeleportUserManagement.Models.Settings;
using TeleportUserManagement.Services;

var builder = WebApplication.CreateBuilder(args);

ConfigurationManager configuration = builder.Configuration;
IWebHostEnvironment environment = builder.Environment;

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddHttpContextAccessor();

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

var encryptionSettings = new EncryptionSettings();
configuration.Bind(nameof(encryptionSettings), encryptionSettings);
if (string.IsNullOrWhiteSpace(encryptionSettings.Key))
  throw new InvalidOperationException(
      "EncryptionSettings:Key must be provided via appsettings or environment variables (EncryptionSettings__Key). It must be a valid 16, 24, or 32-byte Base64-encoded string for AES-128/192/256.");
builder.Services.AddSingleton(encryptionSettings);
builder.Services.AddScoped<IEncDecHelper, EncDecHelper>();

builder.Services.AddSingleton(new AutomaticRetryAttribute() { Attempts = 0 });

string hangfireConnectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddHangfire((provider, config) =>
{
    config.UsePostgreSqlStorage(options => options.UseNpgsqlConnection(hangfireConnectionString));
    config.UseFilter(provider.GetRequiredService<AutomaticRetryAttribute>());
});

builder.Services.AddHangfireServer();
AddVaultServices(builder, configuration);

void AddVaultServices(WebApplicationBuilder builder, ConfigurationManager configuration)
{
    //Configure Vault settings
    builder.Services.Configure<VaultSettings>(configuration.GetSection("VaultSettings"));

    // Register HttpClient for Vault service
    builder.Services.AddHttpClient<IVaultCredentialsService, VaultCredentialsService>((sp, client) =>
    {
        var options = sp.GetRequiredService<IOptions<VaultSettings>>().Value;

        client.BaseAddress = new Uri(options.BaseUrl);
        client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
        client.DefaultRequestHeaders.Add("X-Vault-Token", options.Token);
        client.DefaultRequestHeaders.Accept.Add(
            new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
    });
}

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

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
