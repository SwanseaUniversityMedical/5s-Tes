using Serilog;
using VaultSharp;
using VaultSharp.V1.Commons;

namespace Agent.Api;

public class VaultConfigurationProvider: ConfigurationProvider, IDisposable
{
    private readonly Timer _timer;
    private readonly IVaultClient _vaultClient;
    private readonly string _path;
    private readonly string _mountPoint;

    public VaultConfigurationProvider(IVaultClient vaultClient, string path, string mountPoint, TimeSpan reloadInterval)
    {
        _vaultClient = vaultClient;
        _path = path;
        _mountPoint = mountPoint;

        Load();

        // Check vault for changes in values at the given interval.
        _timer = new Timer(async _ => await LoadAsync(), null, reloadInterval, reloadInterval);
    }

    public async Task LoadAsync()
    {
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
    public static IConfigurationBuilder AddVault(this IConfigurationBuilder builder, IVaultClient client, string path, string mountPoint, TimeSpan reloadInterval)
    {
        return builder.Add(new VaultConfigurationSource
        {
            Client = client,
            Path = path,
            MountPoint = mountPoint,
            ReloadInterval = reloadInterval
        });
    }
}

public class VaultConfigurationSource : IConfigurationSource
{
    public IVaultClient Client { get; set; }
    public string Path { get; set; }
    public string MountPoint { get; set; }
    public TimeSpan ReloadInterval { get; set; }


    public IConfigurationProvider Build(IConfigurationBuilder builder)
    {
        return new VaultConfigurationProvider(Client, Path, MountPoint, ReloadInterval);
    }
}
