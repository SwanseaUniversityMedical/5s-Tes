using Agent.Api.Models;
using Agent.Api.Services;
using FiveSafesTes.Core.Models.ViewModels;
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
        private readonly IOptionsMonitor<TreOnboardingConfig> _onboardingConfig;

        public DoSyncWork(IServiceProvider serviceProvider, IOptionsMonitor<TreOnboardingConfig> configSettings)
        {
            _serviceProvider = serviceProvider;
            _onboardingConfig = configSettings;
        }

        public void Execute()
        {
            if (!_onboardingConfig.CurrentValue.IsConfigurationImported) return;

            using (var scope = _serviceProvider.CreateScope())
            {
                var dareSyncHelper = scope.ServiceProvider.GetRequiredService<IDareSyncHelper>();
                var result = dareSyncHelper.SyncSubmissionWithTre().Result;
            }
        }
    }
}
