using Agent.Api.Services;

namespace Agent.Api
{
    public interface IDoSyncWork
    {
        void Execute();
    }

    public class DoSyncWork : IDoSyncWork
    {
        private readonly IServiceProvider _serviceProvider;

        public DoSyncWork(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        
        }

        public void Execute()
        {
            using (var scope = _serviceProvider.CreateScope())
            {
                var dareSyncHelper = scope.ServiceProvider.GetRequiredService<IDareSyncHelper>();
                var result = dareSyncHelper.SyncSubmissionWithTre().Result;
            }
        }
    }
}
