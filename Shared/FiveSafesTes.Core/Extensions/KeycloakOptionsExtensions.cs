using FiveSafesTes.Core.Models.Settings;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FiveSafesTes.Core.Extensions;

public static class KeycloakOptionsExtensions
{
  public static IServiceCollection AddKeycloakSettings<T>(
    this IServiceCollection services,
    IConfiguration configuration,
    string sectionName)
    where T : BaseKeyCloakSettings
  {
    services.Configure<T>(configuration.GetSection(sectionName));

    services.PostConfigure<T>(options =>
    {
      options.KeycloakDemoMode =
        configuration.GetValue<bool>("KeycloakDemoMode");
    });

    return services;
  }
}
