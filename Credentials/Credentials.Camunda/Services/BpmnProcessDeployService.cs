using Credentials.Camunda.Settings;
using Microsoft.Extensions.Hosting;
using Serilog;


namespace Credentials.Camunda.Services
{
    public class BpmnProcessDeployService : IHostedService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IHostEnvironment _env;
        private readonly CamundaSettings _camundaSettings;

        public BpmnProcessDeployService(IServiceScopeFactory scopeFactory, IHostEnvironment env, CamundaSettings camundaSettings)
        {
            _scopeFactory = scopeFactory;
            _env = env;
            _camundaSettings = camundaSettings;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            const int maxRetries = 10;
            const int delaySeconds = 10;

            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var processModelService = scope.ServiceProvider.GetRequiredService<IProcessModelService>();
                    await processModelService.DeployProcessDefinitionAndDecisionModels();
                    return;
                }
                catch (Exception ex) when (attempt < maxRetries)
                {
                    Log.Warning($"Zeebe not ready (attempt {attempt}/{maxRetries}), retrying in {delaySeconds}s: {ex.Message}");
                    await Task.Delay(TimeSpan.FromSeconds(delaySeconds), cancellationToken);
                }
                catch (Exception ex)
                {
                    Log.Error(ex.ToString());
                    throw;
                }
            }
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
