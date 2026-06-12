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

        // IServiceScopeFactory is used instead of injecting IProcessModelService directly because
        // BpmnProcessDeployService is a singleton (IHostedService) and IProcessModelService is scoped.
        // Injecting a scoped service into a singleton causes a DI lifetime conflict at runtime.
        public BpmnProcessDeployService(IServiceScopeFactory scopeFactory, IHostEnvironment env, CamundaSettings camundaSettings)
        {
            _scopeFactory = scopeFactory;
            _env = env;
            _camundaSettings = camundaSettings;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            // Zeebe may not be ready immediately on startup, so retry up to maxRetries times
            // before giving up and throwing, which will prevent the service from starting.
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
