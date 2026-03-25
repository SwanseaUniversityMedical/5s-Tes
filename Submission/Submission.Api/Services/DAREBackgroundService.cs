using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Options;
using Serilog;
using FiveSafesTes.Core.Models.Enums;
using FiveSafesTes.Core.Models.Settings;
using Submission.Api.Repositories.DbContexts;

namespace Submission.Api.Services
{
    public class DAREBackgroundService : IHostedService, IDisposable
    {
        private Timer _timer;
        HubConnection connection;
        private readonly TREAPISettings _APISettings;
        private readonly IServiceScopeFactory _serviceScopeFactory;

        public DAREBackgroundService(IOptions<TREAPISettings> APISettings, IServiceScopeFactory serviceScopeFactory)
        {
            _APISettings = APISettings.Value;
            _serviceScopeFactory = serviceScopeFactory;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            connection = new HubConnectionBuilder()
    .WithUrl(_APISettings.SignalRAddress)
    .Build();

            connection.Closed += async (error) =>
            {
                await Task.Delay(new Random().Next(0, 5) * 1000);
                await connection.StartAsync();
            };

            connection.On<List<string>>("TREUpdateStatus", UpdateStatusForTre);

            connection.StartAsync();

            return Task.CompletedTask;
        }

        private void UpdateStatusForTre(List<string> varList)
        {
            string trename = varList[0];
            string tesId = varList[1];
            string status = varList[2];

            using (var scope = _serviceScopeFactory.CreateScope())
            {
                var _DbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var tre = _DbContext.Tres.FirstOrDefault(x => x.Name.ToLower() == trename.ToLower());
                if (tre == null)
                {
                    Log.Error("DAREBackgroundService: Unable to find tre");
                    return;
                }

                var sub = _DbContext.Submissions.FirstOrDefault(x => x.TesId == tesId && x.Tre == tre);
                if (sub == null)
                {
                    Log.Error("DAREBackgroundService: Unable to find submission");
                    return;
                }

                Enum.TryParse(status, out StatusType myStatus);
                UpdateSubmissionStatus.UpdateStatusNoSave(sub, myStatus, "");
               

                _DbContext.SaveChanges();
            }
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {

            return Task.CompletedTask;
        }

        public void Dispose()
        {
            _timer?.Dispose();
        }
    }
}
