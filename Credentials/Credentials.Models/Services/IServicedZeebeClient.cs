using Credentials.Models.Models.Zeebe;

namespace Credentials.Models.Services
{
    public interface IServicedZeebeClient
    {
        Task DeployModel(Stream resourceStream, string resourceName);

        Task<DmnResponse> EvaluateDecisionModelAsync(DmnRequest input);

        Task PublishMessageAsync(string messageName, string correlationKey, object variables);

        Task PrintTopologyAsync();
    }
}
