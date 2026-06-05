using Serilog;
using Credentials.Camunda.Extensions;
using Credentials.Camunda.Settings;
using System.Reflection;
using Zeebe.Client.Accelerator.Extensions;
using Credentials.Camunda.Services;
using Serilog.Events;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using FiveSafesTes.Core.Models.Settings;

var builder = WebApplication.CreateBuilder(args);
var configuration = builder.Configuration;
string AppName = typeof(Program).Module.Name.Replace(".dll", "");

Log.Logger = CreateSerilogLogger(configuration);
Log.Information("Camunda logging Start.");

Serilog.ILogger CreateSerilogLogger(IConfiguration configuration)
{
    var seqServerUrl = configuration["Serilog:SeqServerUrl"];
    var seqApiKey = configuration["Serilog:SeqApiKey"];
    var logLevelValue = configuration["Serilog:MinimumLevel:Default"];
    var logLevel = Enum.TryParse<LogEventLevel>(logLevelValue, true, out var parsedLevel)
        ? parsedLevel
        : LogEventLevel.Information;

    if (seqServerUrl == null)
    {
        Log.Error("seqServerUrl is null");
        seqServerUrl = "seqServerUrl == null";
    }

    return new LoggerConfiguration()
        .MinimumLevel.Is(logLevel)
        .Enrich.WithProperty("ApplicationContext", AppName)
        .Enrich.FromLogContext()
        .WriteTo.Console(restrictedToMinimumLevel: logLevel)
        .WriteTo.Seq(seqServerUrl, apiKey: seqApiKey)
        .ReadFrom.Configuration(configuration)
        .CreateLogger();
}

builder.Services.BootstrapZeebe(
    configuration.GetSection("ZeebeBootstrap"),
    Assembly.GetExecutingAssembly()
);

builder.Services.AddZeebeBuilders();
builder.Services.BootstrapZeebe(configuration.GetSection("ZeebeConfiguration"), typeof(Program).Assembly);

builder.Services.Configure<LdapSettings>(configuration.GetSection("LdapSettings"));
builder.Services.Configure<VaultSettings>(configuration.GetSection("VaultSettings"));
builder.Services.AddHttpClient();
builder.Services.AddBusinessServices(configuration);
builder.Services.ConfigureCamunda(configuration);

var treKeyCloakSettings = new TreKeyCloakSettings();
configuration.GetSection("TreKeyCloakSettings").Bind(treKeyCloakSettings);
builder.Services.AddSingleton(treKeyCloakSettings);

var tvp = new TokenValidationParameters
{
    ValidateAudience = true,
    ValidAudiences = treKeyCloakSettings.ValidAudiences?.Trim().Split(',').ToList(),
    ValidIssuer = treKeyCloakSettings.Authority,
    ValidateIssuerSigningKey = true,
    ValidateIssuer = true,
    ValidateLifetime = true
};

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    if (treKeyCloakSettings.Proxy)
        options.BackchannelHttpHandler = treKeyCloakSettings.getProxyHandler;

    options.Authority = treKeyCloakSettings.Authority;
    options.Audience = treKeyCloakSettings.ClientId;
    options.MetadataAddress = treKeyCloakSettings.MetadataAddress;
    options.RequireHttpsMetadata = treKeyCloakSettings.RequireHttpsMetadata;
    options.IncludeErrorDetails = true;
    options.TokenValidationParameters = tvp;
});

builder.Services.AddAuthorization();
builder.Services.AddControllers();

var app = builder.Build();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
await app.RunAsync();
