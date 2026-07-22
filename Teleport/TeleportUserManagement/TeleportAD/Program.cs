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

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
