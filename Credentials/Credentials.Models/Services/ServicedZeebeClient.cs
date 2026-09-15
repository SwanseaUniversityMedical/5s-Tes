using System.Text.Json;
using Credentials.Models.Models.Zeebe;
using Zeebe.Client;

namespace Credentials.Models.Services
{
    public class ServicedZeebeClient : IServicedZeebeClient
    {
        public IZeebeClient _IZeebeClient;
        public IServiceProvider _serviceProvider;

        public ServicedZeebeClient(IZeebeClient IZeebeClient, IServiceProvider serviceProvider)
        {
            _IZeebeClient = IZeebeClient;
            _serviceProvider = serviceProvider;
        }

        public async Task DeployModel(Stream resourceStream, string resourceName)
        {
            var deployResponse = await _IZeebeClient.NewDeployCommand()
                    .AddResourceStream(resourceStream, resourceName)
                    .Send();

            Console.WriteLine($"Deployed model");
        }


        /* DMN model Evaluation */
        public async Task<DmnResponse> EvaluateDecisionModelAsync(DmnRequest input)
        {
            var json = JsonSerializer.Serialize(input.Variables);

            var result = await _IZeebeClient.NewEvaluateDecisionCommand()
                .DecisionId(input.DecisionId)
                .Variables(json)
                .Send();

            var decisionResult = result.DecisionOutput;
            Dictionary<string, object> outputDict;

            var jsonDoc = JsonDocument.Parse(decisionResult);
            var root = jsonDoc.RootElement;

            if (root.ValueKind == JsonValueKind.Object)
            {
                outputDict = JsonSerializer.Deserialize<Dictionary<string, object>>(decisionResult);
            }
            else
            {
                outputDict = new Dictionary<string, object>
                {
                    { "result", root.GetString() }
                };
            }

            return new DmnResponse
            {
                DecisionId = input.DecisionId,
                Result = outputDict
            };
        }


        public async Task PublishMessageAsync(string messageName, string correlationKey, object variables)
        {
            var variablesJson = JsonSerializer.Serialize(variables);

            await _IZeebeClient.NewPublishMessageCommand()
                .MessageName(messageName)
                .CorrelationKey(correlationKey)
                .Variables(variablesJson)
                .Send();

            Console.WriteLine($"Published message: {messageName}");
        }

        // Starts a new Camunda process instance for the given BPMN process ID using the latest deployed version.
        // Variables are serialised to JSON and passed as the initial process variables.
        public async Task CreateProcessInstanceAsync(string bpmnProcessId, object variables)
        {
            var variablesJson = JsonSerializer.Serialize(variables);

            await _IZeebeClient.NewCreateProcessInstanceCommand()
                .BpmnProcessId(bpmnProcessId)
                .LatestVersion()
                .Variables(variablesJson)
                .Send();

            Console.WriteLine($"Created process instance: {bpmnProcessId}");
        }

        private static IServiceProvider serviceProvider;

        public async Task PrintTopologyAsync()
        {
            var topology = await _IZeebeClient.TopologyRequest().Send();
            Console.WriteLine(topology);
        }
    }
}
