using EasyNetQ;
using Hangfire;
using Hangfire.Dashboard;
using Hangfire.Dashboard.BasicAuthorization;
using Hangfire.PostgreSql;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.FeatureManagement;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Newtonsoft.Json;
using Serilog;
using System.Net;
using Zeebe.Client.Accelerator.Extensions;
using System.Reflection;
using Agent.Api;
using Agent.Api.Constants;
using Agent.Api.Models;
using Agent.Api.Repositories.DbContexts;
using Agent.Api.Services;
using Agent.Api.Services.SignalR;
using Credentials.Models.DbContexts;
using FiveSafesTes.Core.Models.Settings;
using FiveSafesTes.Core.Models.ViewModels;
using FiveSafesTes.Core.Rabbit;
using FiveSafesTes.Core.Services;

var builder = WebApplication.CreateBuilder(args);

ConfigurationManager configuration = builder.Configuration;
IWebHostEnvironment environment = builder.Environment;

Log.Logger = CreateSerilogLogger(configuration, environment);
Log.Information("TRE API logging LastStatusUpdate.");
if (configuration["SuppressAntiforgery"] != null && configuration["SuppressAntiforgery"].ToLower() == "true")
{
    Log.Warning("{Function} Disabling Anti Forgery token. Only do if testing", "Main");
    builder.Services.AddAntiforgery(options => options.SuppressXFrameOptionsHeader = true);
    builder.Services.AddDataProtection()
        .PersistKeysToFileSystem(new DirectoryInfo("/root/.aspnet/DataProtection-Keys"))
        .DisableAutomaticKeyGeneration();
}

// Add services to the container.
builder.Services.AddControllersWithViews().AddNewtonsoftJson(options =>
{
    options.SerializerSettings.ReferenceLoopHandling = Newtonsoft.Json.ReferenceLoopHandling.Ignore;
    options.SerializerSettings.PreserveReferencesHandling = PreserveReferencesHandling.Objects;
}
);
;
builder.Services.AddDbContext<ApplicationDbContext>(options => options
    .UseLazyLoadingProxies(true)
    .UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection")
    ));

// Bootstrap Zeebe client if configuration exists (optional for DMN deployment)
if (configuration.GetSection("ZeebeBootstrap").Exists())
{
    builder.Services.BootstrapZeebe(
        configuration.GetSection("ZeebeBootstrap"),
        Assembly.GetExecutingAssembly()
    );
}

AddServices(builder);

builder.Services.AddDbContext<CredentialsDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("CredentialsConnection")));

//Add Dependancies
AddDependencies(builder, configuration);
AddVaultServices(builder, configuration);

builder.Services.Configure<OPASettings>(configuration.GetSection("OPASettings"));
builder.Services.AddTransient(opa => opa.GetService<IOptions<OPASettings>>().Value);
builder.Services.AddScoped<OpaService>();


builder.Services.Configure<RabbitMQSetting>(configuration.GetSection("RabbitMQ"));
builder.Services.AddTransient(cfg => cfg.GetService<IOptions<RabbitMQSetting>>().Value);
var bus =
    builder.Services.AddSingleton(RabbitHutch.CreateBus(
        $"host={configuration["RabbitMQ:HostAddress"]}:{int.Parse(configuration["RabbitMQ:PortNumber"])};virtualHost={configuration["RabbitMQ:VirtualHost"]};username={configuration["RabbitMQ:Username"]};password={configuration["RabbitMQ:Password"]}"));
await SetUpRabbitMQ.DoItTreAsync(configuration["RabbitMQ:HostAddress"], configuration["RabbitMQ:PortNumber"],
    configuration["RabbitMQ:VirtualHost"], configuration["RabbitMQ:Username"], configuration["RabbitMQ:Password"]);

var treKeyCloakSettings = new TreKeyCloakSettings();
configuration.Bind(nameof(treKeyCloakSettings), treKeyCloakSettings);
var keycloakDemomode = configuration["KeycloakDemoMode"].ToLower() == "true";
treKeyCloakSettings.KeycloakDemoMode = keycloakDemomode;
builder.Services.AddSingleton(treKeyCloakSettings);

var dataEgressKeyCloakSettings = new DataEgressKeyCloakSettings();
configuration.Bind(nameof(dataEgressKeyCloakSettings), dataEgressKeyCloakSettings);
dataEgressKeyCloakSettings.KeycloakDemoMode = keycloakDemomode;
builder.Services.AddSingleton(dataEgressKeyCloakSettings);

var submissionKeyCloakSettings = new SubmissionKeyCloakSettings();
configuration.Bind(nameof(submissionKeyCloakSettings), submissionKeyCloakSettings);
submissionKeyCloakSettings.KeycloakDemoMode = keycloakDemomode;
builder.Services.AddSingleton(submissionKeyCloakSettings);

var HasuraSettings = new HasuraSettings();
configuration.Bind(nameof(HasuraSettings), HasuraSettings);
builder.Services.AddSingleton(HasuraSettings);

var minioSettings = new MinioSettings();
configuration.Bind(nameof(MinioSettings), minioSettings);
builder.Services.AddSingleton(minioSettings);


var minioSubSettings = new MinioSubSettings();
configuration.Bind(nameof(MinioSubSettings), minioSubSettings);
builder.Services.AddSingleton(minioSubSettings);

var minioTRESettings = new MinioTRESettings();
configuration.Bind(nameof(MinioTRESettings), minioTRESettings);
builder.Services.AddSingleton(minioTRESettings);


var DmnPath = new FiveSafesTes.Core.Models.DmnPath();
configuration.Bind(nameof(DmnPath), DmnPath);
builder.Services.AddSingleton(DmnPath);



var AuthenticationSetting = new AuthenticationSettings();
configuration.Bind(nameof(AuthenticationSetting), AuthenticationSetting);
builder.Services.AddSingleton(AuthenticationSetting);

var AgentSettings = new AgentSettings();
configuration.Bind(nameof(AgentSettings), AgentSettings);
builder.Services.AddSingleton(AgentSettings);

builder.Services.AddFeatureManagement(
    builder.Configuration.GetSection("Features"));


builder.Services.AddHostedService<ConsumeInternalMessageService>();


builder.Services.AddScoped<IDareClientWithoutTokenHelper, DareClientWithoutTokenHelper>();
builder.Services.AddScoped<IDataEgressClientWithoutTokenHelper, DataEgressClientWithoutTokenHelper>();

string hangfireConnectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddHangfire(config => { config.UsePostgreSqlStorage(hangfireConnectionString); });

builder.Services.AddHangfireServer();
var encryptionSettings = new EncryptionSettings();
configuration.Bind(nameof(encryptionSettings), encryptionSettings);
builder.Services.AddSingleton(encryptionSettings);
builder.Services.AddScoped<IEncDecHelper, EncDecHelper>();
builder.Services.AddScoped<IDareSyncHelper, DareSyncHelper>();
builder.Services.AddScoped<ISubmissionHelper, SubmissionHelper>();
builder.Services.AddScoped<IDoSyncWork, DoSyncWork>();
builder.Services.AddScoped<IDoAgentWork, DoAgentWork>();
builder.Services.AddScoped<IHasuraService, HasuraService>();
builder.Services.AddScoped<IHasuraAuthenticationService, HasuraAuthenticationService>();
builder.Services.AddScoped<IKeyCloakService, KeyCloakService>();

var TVP = new TokenValidationParameters
{
    ValidateAudience = true,
    ValidAudiences = treKeyCloakSettings.ValidAudiences.Trim().Split(',').ToList(),
    ValidIssuer = treKeyCloakSettings.Authority,
    ValidateIssuerSigningKey = true,
    ValidateIssuer = true,
    ValidateLifetime = true
};
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders =
        ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;

    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

builder.Services.AddTransient<IClaimsTransformation, ClaimsTransformerBL>();

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
    .AddJwtBearer(options =>
    {
        Console.WriteLine("TRE Keycloak use proxy = " + treKeyCloakSettings.Proxy.ToString());

        if (treKeyCloakSettings.Proxy)
        {
            Console.WriteLine("TRE API Proxy = " + treKeyCloakSettings.ProxyAddresURL);
            Console.WriteLine("TRE API Proxy bypass = " + treKeyCloakSettings.BypassProxy);
            options.BackchannelHttpHandler = new HttpClientHandler
            {
                UseProxy = true,
                UseDefaultCredentials = true,
                Proxy = new WebProxy()
                {
                    Address = new Uri(treKeyCloakSettings.ProxyAddresURL),
                    BypassList = new[] { treKeyCloakSettings.BypassProxy }
                }
            };
        }

        options.Authority = treKeyCloakSettings.Authority;
        options.Audience = treKeyCloakSettings.ClientId;


        options.MetadataAddress = treKeyCloakSettings.MetadataAddress;

        options.RequireHttpsMetadata = false; // dev only
        options.IncludeErrorDetails = true;

        options.TokenValidationParameters = TVP;
    });

Log.Information(
    "{Function} Authority {Authority}, MetaAddress {MetaAddress}, Audience {Audience}, ValidAudiences {ValidAudiences}",
    "Program", treKeyCloakSettings.Authority, treKeyCloakSettings.MetadataAddress, treKeyCloakSettings.ClientId,
    treKeyCloakSettings.ValidAudiences);
// - authorize here


// Enable CORS
var MyAllowSpecificOrigins = "_myAllowSpecificOrigins";
builder.Services.AddCors(options =>
{
    options.AddPolicy(name: MyAllowSpecificOrigins,
        policy =>
        {
            policy.WithOrigins(
                    configuration["TreAPISettings:Address"]
                )
                .AllowAnyMethod()
                .AllowAnyHeader()
                .AllowCredentials();
        });
});

var app = builder.Build();
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedProto
});
// --- Session Token

// Configure the HTTP request pipeline.

app.UseSwagger();

app.UseSwaggerUI(c =>
{
    c.EnableValidator(null);
    c.SwaggerEndpoint("/swagger/v1/swagger.json", $"{environment.ApplicationName} v1");
    c.OAuthClientId(treKeyCloakSettings.ClientId);
    c.OAuthClientSecret(treKeyCloakSettings.ClientSecret);
    c.OAuthAppName(treKeyCloakSettings.ClientId);
});
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    //app.UseHsts();
}


using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    var credDb = scope.ServiceProvider.GetRequiredService<CredentialsDbContext>();
    var encDec = scope.ServiceProvider.GetRequiredService<IEncDecHelper>();
    IFeatureManager featureManager = app.Services.GetRequiredService<IFeatureManager>();
    db.Database.Migrate();
    var initialiser = new DataInitaliser(db, encDec);
    if (await featureManager.IsEnabledAsync(FeatureFlags.DemoAllInOne))
    {
        Log.Information("Demo mode is on, seeding data...");
        initialiser.SeedAllInOneData(configuration["DemoModeDefaultP"]);
    }
    credDb.Database.Migrate();
}


//app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
//app.UseCookiePolicy(new CookiePolicyOptions
//{
//    Secure = CookieSecurePolicy.Always
//});

app.UseCors(MyAllowSpecificOrigins);
app.UseAuthentication();
app.UseAuthorization();
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");


Serilog.ILogger CreateSerilogLogger(ConfigurationManager configuration, IWebHostEnvironment environment)
{
    var seqServerUrl = configuration["Serilog:SeqServerUrl"];
    var seqApiKey = configuration["Serilog:SeqApiKey"];


    return new LoggerConfiguration()
        .MinimumLevel.Verbose()
        .Enrich.WithProperty("ApplicationContext", environment.ApplicationName)
        .Enrich.FromLogContext()
        .WriteTo.Console()
        .WriteTo.Seq(seqServerUrl, apiKey: seqApiKey)
        .ReadFrom.Configuration(configuration)
        .CreateLogger();
}

void AddDependencies(WebApplicationBuilder builder, ConfigurationManager configuration)
{
    builder.Services.AddHttpContextAccessor();

    builder.Services.AddScoped<IMinioTreHelper, MinioTreHelper>();
    builder.Services.AddScoped<IMinioSubHelper, MinioSubHelper>();
    builder.Services.AddScoped<ISignalRService, SignalRService>();
    builder.Services.AddMvc().AddControllersAsServices();
}

void AddVaultServices(WebApplicationBuilder builder, ConfigurationManager configuration)
{
    //Configure Vault settings
    builder.Services.Configure<VaultSettings>(
        configuration.GetSection("VaultSettings"));

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


void AddServices(WebApplicationBuilder builder)
{
    ServicePointManager.ServerCertificateValidationCallback +=
        (sender, cert, chain, sslPolicyErrors) => true;
    builder.Services.AddHttpClient();

    // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSignalR();

    // DMN Service for managing DMN files
    builder.Services.AddScoped<IDmnService, DmnService>();

    // Zeebe Client for DMN deployment and evaluation
    builder.Services.AddScoped<Credentials.Models.Services.IServicedZeebeClient, Credentials.Models.Services.ServicedZeebeClient>();

    //TODO
    builder.Services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new OpenApiInfo { Title = environment.ApplicationName, Version = "v1" });
        var securityScheme = new OpenApiSecurityScheme
        {
            Name = "JWT Authentication",
            Description = "Enter JWT token.",
            In = ParameterLocation.Header,
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            Reference = new OpenApiReference
            {
                Id = JwtBearerDefaults.AuthenticationScheme,
                Type = ReferenceType.SecurityScheme
            }
        };

        c.AddSecurityDefinition(securityScheme.Reference.Id, securityScheme);
        c.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                { securityScheme, new string[] { } }
            });
    }
    );

    if (!string.IsNullOrEmpty(configuration.GetConnectionString("DefaultConnection")))
    {
        builder.Services.AddDbContext<ApplicationDbContext>(options => options.UseNpgsql(
            builder.Configuration.GetConnectionString("DefaultConnection")
        ));
    }
}

//for SignalR
app.UseCors();
app.MapHub<SignalRService>("/signalRHub",
        options => { options.Transports = HttpTransportType.WebSockets | HttpTransportType.LongPolling; })
    .RequireCors(MyAllowSpecificOrigins);

//Hangfire
var jobSettings = new JobSettings();
configuration.Bind(nameof(JobSettings), jobSettings);
var extHangfire = configuration["EnableExternalHangfire"];

if (extHangfire != null && extHangfire.ToLower() == "true")
{
    app.UseHangfireDashboard("/hangfire", new DashboardOptions
    {
        Authorization = new List<IDashboardAuthorizationFilter>()
        {
            //new LocalRequestsOnlyAuthorizationFilter(),
            new BasicAuthAuthorizationFilter(new BasicAuthAuthorizationFilterOptions
            {
                RequireSsl = false,
                SslRedirect = false,
                LoginCaseSensitive = false,
                Users = new[]
                {
                    new BasicAuthAuthorizationUser
                    {
                        Login = "admin",
                        PasswordClear = "password123",
                    },
                },
            }),
        },
        //IsReadOnlyFunc = (DashboardContext context) => true,
    });
}
else
{
    app.UseHangfireDashboard();
}


const string syncJobName = "Sync Projects and Membership";
if (jobSettings.syncSchedule == 0)
    RecurringJob.RemoveIfExists(syncJobName);
else
    RecurringJob.AddOrUpdate<IDoSyncWork>(syncJobName, x => x.Execute(), Cron.MinuteInterval(jobSettings.syncSchedule));

const string scanJobName = "Sync Submissions";

if (jobSettings.scanSchedule == 0)
    RecurringJob.RemoveIfExists(scanJobName);
else
    RecurringJob.AddOrUpdate<IDoAgentWork>(scanJobName,
        x => x.Execute(),
        Cron.MinuteInterval(jobSettings.scanSchedule));


if (HasuraSettings.IsEnabled)
{
    RecurringJob.AddOrUpdate<IHasuraService>(a => a.Run(), Cron.HourInterval(4));
}


var port = app.Environment.WebRootPath;
Console.WriteLine("Application is running on port: " + port);

Log.Information($"minioTRESettings Url> {minioTRESettings.Url}");
app.Run();
