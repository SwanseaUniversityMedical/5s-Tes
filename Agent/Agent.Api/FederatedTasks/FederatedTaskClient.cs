using Agent.Api.Services;
using FiveSafesTes.Core.Models.APISimpleTypeReturns;
using FiveSafesTes.Core.Models.FederatedTasks;
using FiveSafesTes.Core.Models.Tes;

namespace Agent.Api.FederatedTasks
{
    public class FederatedTaskClient : IFederatedTaskClient
    {
        private readonly IDareClientWithoutTokenHelper _dareClient;

        public FederatedTaskClient(IDareClientWithoutTokenHelper dareClient)
        {
            _dareClient = dareClient;
        }

        public bool IsConfigured => _dareClient.CheckCredsAreAvailable();

        public async Task<List<TesTask>> GetPendingAsync()
        {
            var tasks = await _dareClient.CallAPIWithoutModel<List<TesTask>>("/api/FederatedTask/GetPendingTasksForTre");
            return tasks ?? new List<TesTask>();
        }

        public async Task<bool> ReportAsync(FederatedTaskResultReport report)
        {
            var result = await _dareClient.CallAPI<FederatedTaskResultReport, BoolReturn>("/api/FederatedTask/ReportResult", report);
            return result?.Result ?? false;
        }
    }
}
