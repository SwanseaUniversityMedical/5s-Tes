using Agent.Api.Models;
using Agent.Api.Services;
using Microsoft.Extensions.Options;

namespace Agent.Api
{
    public interface IDoSyncWork
    {
        void Execute();
    }

    public class DoSyncWork : IDoSyncWork
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IOptionsMonitor<VaultConfigSettings> _configSettings;

        public DoSyncWork(IServiceProvider serviceProvider, IOptionsMonitor<VaultConfigSettings> configSettings)
        {
            _serviceProvider = serviceProvider;
            _configSettings = configSettings;
        }

        public void Execute()
        {
            if (!_configSettings.CurrentValue.IsConfigurationImported) return;

            using (var scope = _serviceProvider.CreateScope())
            {
                var dareSyncHelper = scope.ServiceProvider.GetRequiredService<IDareSyncHelper>();
                var result = dareSyncHelper.SyncSubmissionWithTre().Result;
            }
        }
    }
}
