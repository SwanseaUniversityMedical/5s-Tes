using System.Reflection;
using Credentials.Camunda.Extensions;
using Credentials.Camunda.Models;
using Credentials.Camunda.Services;
using Credentials.Camunda.Settings;
using Serilog;
using Serilog.Events;
using Zeebe.Client;
using Zeebe.Client.Accelerator.Extensions;

var builder = WebApplication.CreateBuilder(args);
var configuration = GetConfiguration();
string AppName = typeof(Program).Module.Name.Replace(".dll", "");

ConfigurationManager configurations = builder.Configuration;

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

await Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        services.BootstrapZeebe(
          configuration.GetSection("ZeebeBootstrap"),
          Assembly.GetExecutingAssembly()
      );

        services.AddZeebeBuilders();
        services.BootstrapZeebe(configuration.GetSection("ZeebeConfiguration"), typeof(Program).Assembly);


        services.Configure<LdapSettings>(configuration.GetSection("LdapSettings"));
        services.Configure<VaultSettings>(configuration.GetSection("VaultSettings"));

      var FlowSettings = new FlowSettings();
      configuration.GetSection(nameof(FlowSettings)).Bind(FlowSettings);
      services.AddSingleton(FlowSettings);


      services.AddHttpClient();
        services.AddBusinessServices(configuration);
        services.ConfigureCamunda(configuration);        

    })
    .Build()
    .RunAsync();

/// <summary>
/// GetConfiguration
/// </summary>
IConfiguration GetConfiguration()
{
    var a = Directory.GetCurrentDirectory();
    var builder = new ConfigurationBuilder()
        .SetBasePath(Directory.GetCurrentDirectory())
        .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
        .AddJsonFile($"appsettings.development.json", optional: true, reloadOnChange: true)
        .AddEnvironmentVariables();

    return builder.Build();
}
