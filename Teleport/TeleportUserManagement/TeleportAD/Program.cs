using Agent.Api.Constants;
using Agent.Api.Repositories.DbContexts;
using FiveSafesTes.Core.Models.Settings;
using FiveSafesTes.Core.Models.ViewModels;
using FiveSafesTes.Core.Services;
using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.Extensions.Options;
using Microsoft.FeatureManagement;
using TeleportUserManagement.Models.Settings;
using TeleportUserManagement.Services;

var builder = WebApplication.CreateBuilder(args);

ConfigurationManager configuration = builder.Configuration;
IWebHostEnvironment environment = builder.Environment;

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddSingleton<ILdapService, LdapService>();
builder.Services.AddSingleton<IUserService, UserService>();

var activeDirectorySettings = new ActiveDirectorySettings();
configuration.Bind(nameof(ActiveDirectorySettings), activeDirectorySettings);
builder.Services.AddSingleton(activeDirectorySettings);

var jobSettings = new JobSettings();
configuration.Bind(nameof(JobSettings), jobSettings);
builder.Services.AddSingleton(jobSettings);

builder.Services.AddSingleton(new AutomaticRetryAttribute() { Attempts = 3 });

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

app.Run();
