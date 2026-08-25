using System.Net.Http.Headers;
using Credentials.Camunda.ProcessHandlers;
using Credentials.Camunda.Services;
using Credentials.Camunda.Settings;
using Credentials.Models.DbContexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;
using IPostgreSQLUserManagementService = Credentials.Camunda.Services.IPostgreSQLUserManagementService;
using IVaultCredentialsService = Credentials.Camunda.Services.IVaultCredentialsService;
using PostgreSQLUserManagementService = Credentials.Camunda.Services.PostgreSQLUserManagementService;
using Services_IPostgreSQLUserManagementService = Credentials.Camunda.Services.IPostgreSQLUserManagementService;
using Services_IVaultCredentialsService = Credentials.Camunda.Services.IVaultCredentialsService;
using Services_PostgreSQLUserManagementService = Credentials.Camunda.Services.PostgreSQLUserManagementService;
using Services_VaultCredentialsService = Credentials.Camunda.Services.VaultCredentialsService;
using VaultCredentialsService = Credentials.Camunda.Services.VaultCredentialsService;


namespace Credentials.Camunda.Extensions
{
    public static class ServiceExtensions
    {
        public static void AddBusinessServices(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment) // add services here
        {


            services.AddHttpClient();
            services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();

            services.Configure<VaultSettings>(configuration.GetSection("VaultSettings"));

            services.AddHttpClient<Services_IVaultCredentialsService, Services_VaultCredentialsService>((sp, client) =>
            {
                var settings = sp.GetRequiredService<IOptions<VaultSettings>>().Value;

                client.BaseAddress = new Uri(settings.BaseUrl);
                client.Timeout = TimeSpan.FromSeconds(settings.TimeoutSeconds);
                client.DefaultRequestHeaders.Clear();
                client.DefaultRequestHeaders.Add("X-Vault-Token", settings.Token);
                client.DefaultRequestHeaders.Accept.Add(
                    new MediaTypeWithQualityHeaderValue("application/json"));
            });

            services.AddDbContext<CredentialsDbContext>(options =>
                options.UseNpgsql(GetPostgresSslString(configuration.GetConnectionString("CredentialsConnection"), environment)));

        }

        public static void ConfigureCamunda(this IServiceCollection services, IConfiguration configuration)
        {
            var camundaSettings = new CamundaSettings();
            configuration.Bind(nameof(camundaSettings), camundaSettings);
            services.AddSingleton(camundaSettings);

            var DmnPath = new FiveSafesTes.Core.Models.DmnPath();
            configuration.Bind(nameof(DmnPath), DmnPath);
            services.AddSingleton(DmnPath);

            services.AddHostedService<BpmnProcessDeployService>();
            services.AddScoped<IProcessModelService, ProcessModelService>();

            services.AddScoped<Credentials.Models.Services.IServicedZeebeClient, Credentials.Models.Services.ServicedZeebeClient>();

            services.AddScoped<Services_IPostgreSQLUserManagementService, Services_PostgreSQLUserManagementService>();
            services.AddScoped<CreatePostgresUserHandler>();

            services.AddScoped<ILdapUserManagementService, LdapUserManagementService>();
            services.AddScoped<CreateTrinoUserHandler>();

            services.AddScoped<IEphemeralCredentialsService, EphemeralCredentialsService>();

            services.AddScoped<CreateTreCredentialsHandler>();
            services.AddScoped<DeleteTreCredentialsHandler>();
        }

        public static string GetPostgresSslString(string connectionString, IHostEnvironment environment)
        {
            var builder = new NpgsqlConnectionStringBuilder(connectionString);
            if (!environment.IsDevelopment()) builder.SslMode = SslMode.Require;
            return builder.ConnectionString;
        }
    }
}
