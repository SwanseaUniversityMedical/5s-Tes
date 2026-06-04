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
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var processModelService = scope.ServiceProvider.GetRequiredService<IProcessModelService>();
                await processModelService.DeployProcessDefinitionAndDecisionModels();
            }
            catch (Exception ex)
            {
                Log.Error(ex.ToString());
                throw;
            }
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
