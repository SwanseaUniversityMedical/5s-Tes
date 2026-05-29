using FiveSafesTes.Core.Models.ViewModels;
using Microsoft.Extensions.Options;
using Serilog;
using VaultSharp;
using VaultSharp.V1.Commons;

namespace Agent.Api;

public class VaultConfigurationProvider: ConfigurationProvider, IDisposable
{
    private readonly Timer _timer;
    private readonly IOptionsMonitor<TreOnboardingConfig> _onboardingConfig;
    private readonly IVaultClient _vaultClient;
    private readonly string _path;
    private readonly string _mountPoint;

    public VaultConfigurationProvider(IVaultClient vaultClient, IServiceProvider serviceProvider, string path, string mountPoint, TimeSpan reloadInterval)
    {
        _vaultClient = vaultClient;
        _path = path;
        _mountPoint = mountPoint;
        _onboardingConfig = serviceProvider.GetRequiredService<IOptionsMonitor<TreOnboardingConfig>>();

        Load();

        // Check vault for changes in values at the given interval.
        _timer = new Timer(async _ => await LoadAsync(), null, reloadInterval, reloadInterval);
    }

    /// <summary>
    /// Reload the configuration to apply any changes to values in Vault.
    /// </summary>
    /// <param name="bypassConfigCheck">When true, allows the loading to proceed irrespective of whether config has been uploaded to Vault.</param>
    public async Task LoadAsync(bool bypassConfigCheck = false)
    {
        // Onboarding configuration can never be true until config is reloaded, so we need a way of bypassing this check when we're uploading the config.
        if (!_onboardingConfig.CurrentValue.IsConfigurationImported && !bypassConfigCheck)
        {
            // Don't try to read configuration values from vault before the config has been uploaded.
            Log.Warning("VaultConfigurationProvider:LoadAsync - Configuration not yet set");
            return;
        }

        try
        {
            Secret<SecretData> secret = await _vaultClient.V1.Secrets.KeyValue.V2.ReadSecretAsync(_path, mountPoint: _mountPoint);
            IDictionary<string, object> data = secret.Data.Data;

            Dictionary<string, string?> newData = data.ToDictionary(k => k.Key, v => v.Value?.ToString());

            // Do not reload if there are no changes to the values in vault.
            if (!Data.SequenceEqual(newData))
            {
                Data = newData;
                OnReload();
            }
        }
        catch (Exception ex)
        {
            Log.Error("VaultConfigurationProvider:LoadAsync - " + ex.Message);
        }
    }

    public override void Load()
    {
        LoadAsync().GetAwaiter().GetResult();
    }

    public void Dispose()
    {
        _timer?.Dispose();
    }
}

/// <summary>
/// Extend the configuration builder with the AddVault method, allowing us to register Vault as a configuration source.
/// </summary>
public static class VaultConfigurationExtensions
{
    public static IConfigurationBuilder AddVault(this IConfigurationBuilder builder, IVaultClient client, IServiceProvider serviceProvider, string path, string mountPoint, TimeSpan reloadInterval)
    {
        return builder.Add(new VaultConfigurationSource
        {
            Client = client,
            Path = path,
            MountPoint = mountPoint,
            ReloadInterval = reloadInterval,
            ServiceProvider = serviceProvider
        });
    }
}

public class VaultConfigurationSource : IConfigurationSource
{
    public IVaultClient Client { get; set; }
    public IServiceProvider ServiceProvider { get; set; }
    public string Path { get; set; }
    public string MountPoint { get; set; }
    public TimeSpan ReloadInterval { get; set; }


    public IConfigurationProvider Build(IConfigurationBuilder builder)
    {
        return new VaultConfigurationProvider(Client, ServiceProvider, Path, MountPoint, ReloadInterval);
    }
}
