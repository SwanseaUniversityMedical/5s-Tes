using Credentials.Camunda.Settings;
using Microsoft.Extensions.Hosting;
using Serilog;


namespace Credentials.Camunda.Services
{
    public class BpmnProcessDeployService : IHostedService
    {
        private readonly IProcessModelService _processModelService;
        private readonly IHostEnvironment _env;
        private readonly CamundaSettings _camundaSettings;

        public BpmnProcessDeployService(IProcessModelService processModelService, IHostEnvironment env, CamundaSettings camundaSettings)
        {
            _processModelService = processModelService;
            _env = env;
            _camundaSettings = camundaSettings;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            //var mn = Environment.MachineName;
            //bool test = _env.IsDevelopment();
            //test = true;
          
                try
                {
                    await _processModelService.DeployProcessDefinitionAndDecisionModels();
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
