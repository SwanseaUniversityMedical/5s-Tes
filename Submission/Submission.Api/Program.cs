using Submission.Api.Repositories.DbContexts;
using Submission.Api.Services.Contract;
using Submission.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;
using System.Net;

using Newtonsoft.Json;
using EasyNetQ;
using FiveSafesTes.Core.Models;
using FiveSafesTes.Core.Models.Settings;
using FiveSafesTes.Core.Models.ViewModels;
using FiveSafesTes.Core.Rabbit;
using FiveSafesTes.Core.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using NETCore.MailKit.Extensions;
using NETCore.MailKit.Infrastructure.Internal;
using Microsoft.AspNetCore.DataProtection;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHealthChecks();

ConfigurationManager configuration = builder.Configuration;
IWebHostEnvironment environment = builder.Environment;

Log.Logger = CreateSerilogLogger(configuration, environment);
Log.Information("API logging LastStatusUpdate.");

// Add services to the container.
builder.Services.AddControllersWithViews().AddNewtonsoftJson(options =>
    {
        options.SerializerSettings.ReferenceLoopHandling = Newtonsoft.Json.ReferenceLoopHandling.Ignore;
        options.SerializerSettings.PreserveReferencesHandling = PreserveReferencesHandling.Objects;
    }
); ;
builder.Services.AddDbContext<ApplicationDbContext>(options => options
    .UseLazyLoadingProxies(true)
    .UseNpgsql(
    builder.Configuration.GetConnectionString("DefaultConnection")
));
if (configuration["SuppressAntiforgery"] != null && configuration["SuppressAntiforgery"].ToLower() == "true")
{
    Log.Warning("{Function} Disabling Anti Forgery token. Only do if testing", "Main");
    builder.Services.AddAntiforgery(options => options.SuppressXFrameOptionsHeader = true);
    builder.Services.AddDataProtection()
        .PersistKeysToFileSystem(new DirectoryInfo("/root/.aspnet/DataProtection-Keys"))
        .DisableAutomaticKeyGeneration();
}
//Add Services
AddServices(builder);

//Add Dependancies
AddDependencies(builder, configuration);
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders =
        ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;

    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});



builder.Services.Configure<RabbitMQSetting>(configuration.GetSection("RabbitMQ"));
builder.Services.AddTransient(cfg => cfg.GetService<IOptions<RabbitMQSetting>>().Value);
var bus =
builder.Services.AddSingleton(RabbitHutch.CreateBus($"host={configuration["RabbitMQ:HostAddress"]}:{int.Parse(configuration["RabbitMQ:PortNumber"])};virtualHost={configuration["RabbitMQ:VirtualHost"]};username={configuration["RabbitMQ:Username"]};password={configuration["RabbitMQ:Password"]}"));
await SetUpRabbitMQ.DoItSubmissionAsync(configuration["RabbitMQ:HostAddress"], configuration["RabbitMQ:PortNumber"], configuration["RabbitMQ:VirtualHost"], configuration["RabbitMQ:Username"], configuration["RabbitMQ:Password"]);

var submissionKeyCloakSettings = new SubmissionKeyCloakSettings();
configuration.Bind(nameof(submissionKeyCloakSettings), submissionKeyCloakSettings);
var keycloakDemomode = configuration["KeycloakDemoMode"].ToLower() == "true";
var demomode = configuration["DemoMode"].ToLower() == "true";
submissionKeyCloakSettings.KeycloakDemoMode = keycloakDemomode;
builder.Services.AddSingleton(submissionKeyCloakSettings);

builder.Services.Configure<KestrelServerOptions>(options =>
{
    options.Limits.MaxRequestBodySize = int.MaxValue; // Adjust this as needed
});

var minioSettings = new MinioSettings();
configuration.Bind(nameof(MinioSettings), minioSettings);
builder.Services.AddSingleton(minioSettings);


var emailSettings = new EmailSettings();
configuration.Bind(nameof(emailSettings), emailSettings);
builder.Services.AddSingleton(emailSettings);


builder.Services.AddHostedService<ConsumeInternalMessageService>();
var TVP = new TokenValidationParameters
{
    ValidateAudience = true,
    ValidAudiences = submissionKeyCloakSettings.ValidAudiences.Trim().Split(',').ToList(),
    ValidIssuer = submissionKeyCloakSettings.Authority,
    ValidateIssuerSigningKey = true,
    ValidateIssuer = false,
    ValidateLifetime = true
};
Log.Information($"Check TokenValidationParams for Issuer {submissionKeyCloakSettings.Authority}");

builder.Services.AddTransient<IClaimsTransformation, ClaimsTransformerBL>();


builder.Services.AddMailKit(optionBuilder =>
{
    optionBuilder.UseMailKit(new MailKitOptions
    {
        Server = emailSettings.Host,
        Port = emailSettings.Port,
        SenderName = emailSettings.FromDisplayName,
        SenderEmail = emailSettings.FromAddress,

        // can be optional with no authentication 
        //Account = Configuration["Account"],
        //Password = Configuration["Password"],
        // enable ssl or tls
        Security = emailSettings.EnableSSL
    });
});

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
    .AddJwtBearer(options =>
    {
        if (submissionKeyCloakSettings.Proxy)
        {
            options.BackchannelHttpHandler = new HttpClientHandler
            {
                UseProxy = true,
                UseDefaultCredentials = true,
                Proxy = new WebProxy()
                {
                    Address = new Uri(submissionKeyCloakSettings.ProxyAddresURL),
                    BypassList = new[] { submissionKeyCloakSettings.BypassProxy }
                }
            };
        }
        
        options.Authority = submissionKeyCloakSettings.Authority;
        options.Audience = submissionKeyCloakSettings.ClientId;          
        options.MetadataAddress = submissionKeyCloakSettings.MetadataAddress;

        options.RequireHttpsMetadata = false; // dev only
        options.IncludeErrorDetails = true;

        options.TokenValidationParameters = TVP;
        options.Events = new JwtBearerEvents
        {
            OnForbidden = context =>
            {
                //Log.Information("ONFORBIDDEN START");
                //Log.Information("HttpContext.Connection.RemoteIpAddress : {RemoteIpAddress}",
                //    context.HttpContext.Connection.RemoteIpAddress);
                //Log.Information("HttpContext.Connection.RemotePort : {RemotePort}",
                //    context.HttpContext.Connection.RemotePort);
                //Log.Information("HttpContext.Request.Scheme : {Scheme}", context.HttpContext.Request.Scheme);
                //Log.Information("HttpContext.Request.Host : {Host}", context.HttpContext.Request.Host);

                //foreach (var header in context.HttpContext.Request.Headers)
                //{
                //    Log.Information("Request Header {key} - {value}", header.Key, header.Value);
                //}

                //foreach (var header in context.HttpContext.Response.Headers)
                //{
                //    Log.Information("Response Header {key} - {value}", header.Key, header.Value);
                //}
                //Log.Information("ONFORBIDDEN END");
                return context.Response.CompleteAsync();
            },
            OnTokenValidated = context =>
            {
                //Log.Information("ONTOKENVALIDATED START");
                //Log.Information("HttpContext.Connection.RemoteIpAddress : {RemoteIpAddress}",
                //    context.HttpContext.Connection.RemoteIpAddress);
                //Log.Information("HttpContext.Connection.RemotePort : {RemotePort}",
                //    context.HttpContext.Connection.RemotePort);
                //Log.Information("HttpContext.Request.Scheme : {Scheme}", context.HttpContext.Request.Scheme);
                //Log.Information("HttpContext.Request.Host : {Host}", context.HttpContext.Request.Host);

                //foreach (var header in context.HttpContext.Request.Headers)
                //{
                //    Log.Information("Request Header {key} - {value}", header.Key, header.Value);
                //}

                //foreach (var header in context.HttpContext.Response.Headers)
                //{
                //    Log.Information("Response Header {key} - {value}", header.Key, header.Value);
                //}
                //Log.Information("ONTOKENVALIDATED END");
                //// Log the issuer claim from the token
                //var issuer = context.Principal.FindFirst("iss")?.Value;
                //Log.Information("Token Issuer: {Issuer}", issuer);
                //var audience = context.Principal.FindFirst("aud")?.Value;
                //Log.Information("Token Audience: {Audience}", audience);
                return Task.CompletedTask;
            },
            OnAuthenticationFailed = context =>
            {
                //Log.Information("ONAUTHFAILED START");
                //Log.Information("HttpContext.Connection.RemoteIpAddress : {RemoteIpAddress}",
                //    context.HttpContext.Connection.RemoteIpAddress);
                //Log.Information("HttpContext.Connection.RemotePort : {RemotePort}",
                //    context.HttpContext.Connection.RemotePort);
                //Log.Information("HttpContext.Request.Scheme : {Scheme}", context.HttpContext.Request.Scheme);
                //Log.Information("HttpContext.Request.Host : {Host}", context.HttpContext.Request.Host);

                //foreach (var header in context.HttpContext.Request.Headers)
                //{
                //    Log.Information("Request Header {key} - {value}", header.Key, header.Value);
                //}

                //foreach (var header in context.HttpContext.Response.Headers)
                //{
                //    Log.Information("Response Header {key} - {value}", header.Key, header.Value);
                //}
                //Log.Information("ONAUTHFAILED END");
                //Log.Error("{Function}: {ex}", "OnAuthFailed", context.Exception.Message);
                //Log.Error("Auth failed event: {event}", context.Request.Headers);
                context.Response.StatusCode = 401;
                return context.Response.CompleteAsync();
            },
            OnMessageReceived = context =>
            {
                //Log.Information("ONMESSAGERECEIVED START");
                //Log.Information("HttpContext.Connection.RemoteIpAddress : {RemoteIpAddress}",
                //    context.HttpContext.Connection.RemoteIpAddress);
                //Log.Information("HttpContext.Connection.RemotePort : {RemotePort}",
                //    context.HttpContext.Connection.RemotePort);
                //Log.Information("HttpContext.Request.Scheme : {Scheme}", context.HttpContext.Request.Scheme);
                //Log.Information("HttpContext.Request.Host : {Host}", context.HttpContext.Request.Host);

                //foreach (var header in context.HttpContext.Request.Headers)
                //{
                //    Log.Information("Request Header {key} - {value}", header.Key, header.Value);
                //}

                //foreach (var header in context.HttpContext.Response.Headers)
                //{
                //    Log.Information("Response Header {key} - {value}", header.Key, header.Value);
                //}
                //Log.Information("ONMESSAGERECEVIED END");
                string accessToken = context.Request.Query["access_token"];
                PathString path = context.HttpContext.Request.Path;

                if (
                    !string.IsNullOrEmpty(accessToken) &&
                    path.StartsWithSegments("/api/SignalRHub")
                )
                {
                    context.Token = accessToken;
                }

                return Task.CompletedTask;
            },
            OnChallenge = context =>
            {
                //Log.Information("ONCHALLENGE START");
                //Log.Information("HttpContext.Connection.RemoteIpAddress : {RemoteIpAddress}",
                //    context.HttpContext.Connection.RemoteIpAddress);
                //Log.Information("HttpContext.Connection.RemotePort : {RemotePort}",
                //    context.HttpContext.Connection.RemotePort);
                //Log.Information("HttpContext.Request.Scheme : {Scheme}", context.HttpContext.Request.Scheme);
                //Log.Information("HttpContext.Request.Host : {Host}", context.HttpContext.Request.Host);

                //foreach (var header in context.HttpContext.Request.Headers)
                //{
                //    Log.Information("Request Header {key} - {value}", header.Key, header.Value);
                //}

                //foreach (var header in context.HttpContext.Response.Headers)
                //{
                //    Log.Information("Response Header {key} - {value}", header.Key, header.Value);
                //}
                //Log.Information("ONCHALLENGE END");
                return Task.CompletedTask;
            }
        };
    });



// - authorize here
builder.Services.AddAuthorization(options =>
{

});

var app = builder.Build();

var serviceScopeFactory = app.Services.GetRequiredService<IServiceScopeFactory>();
app.MapHealthChecks("/health");

app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedProto
});
// --- Session Token


// Swagger
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.EnableValidator(null);
    c.SwaggerEndpoint("/swagger/ga4gh-tes/swagger.json", $"GA4GH TES API");
    c.SwaggerEndpoint("/swagger/v1/swagger.json", $"{environment.ApplicationName} v1");
    c.OAuthClientId(submissionKeyCloakSettings.ClientId);
    c.OAuthClientSecret(submissionKeyCloakSettings.ClientSecret);
    c.OAuthAppName(submissionKeyCloakSettings.ClientId);
});


// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    //app.UseDeveloperExceptionPage();
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}




using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    var keytoken = scope.ServiceProvider.GetRequiredService<IKeycloakTokenApiHelper>();
    var miniosettings = scope.ServiceProvider.GetRequiredService<MinioSettings>();
    var miniohelper = scope.ServiceProvider.GetRequiredService<IMinioHelper>();
    var userService = scope.ServiceProvider.GetRequiredService<IKeycloakMinioUserService>();

    db.Database.Migrate();
    var initialiser = new DataInitialiser(miniosettings, db, keytoken, userService, miniohelper);
    if (demomode)
    {
        initialiser.SeedAllInOneData();
    }
}



//app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    db.Database.Migrate();
}
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


    
    builder.Services.AddScoped<IMinioHelper, MinioHelper>();
    builder.Services.AddScoped<IKeycloakMinioUserService, KeycloakMinioUserService>();
    builder.Services.AddScoped<IKeycloakTokenApiHelper, KeycloakTokenApiHelper>();
    builder.Services.AddScoped<IKeyCloakService, KeyCloakService>();
    builder.Services.AddScoped<IDareEmailService, DareEmailService>();
    


}


/// <summary>
/// Add Services
/// </summary>
async void AddServices(WebApplicationBuilder builder)
{
    builder.Services.AddHttpClient();
    // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSignalR();
    builder.Services.Configure<TREAPISettings>(configuration.GetSection("TREAPI"));
    builder.Services.AddHostedService<DAREBackgroundService>();

    //TODO
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(c =>
      {
        c.EnableAnnotations();
        c.UseOneOfForPolymorphism();
        // GA4GH TES API Docs definition
        c.SwaggerDoc("ga4gh-tes", new()
          {
            Title = "GA4GH TES API",
            Version = "v1.1.0",
            Description = "A GA4GH TES API implementation"
          }
        );
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

app.Run();

