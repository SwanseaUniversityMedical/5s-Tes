using Serilog;
using Credentials.Camunda.Extensions;
using Credentials.Camunda.Settings;
using System.Reflection;
using Zeebe.Client.Accelerator.Extensions;
using Zeebe.Client;
using Credentials.Camunda.Services;
using Serilog.Events;

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
builder.Services.Configure<Credentials.Camunda.Settings.VaultSettings>(configuration.GetSection("VaultSettings"));
builder.Services.AddHttpClient();
builder.Services.AddBusinessServices(configuration);
builder.Services.ConfigureCamunda(configuration);

var app = builder.Build();
await app.RunAsync();
