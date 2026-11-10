using FiveSafesTes.Core.Models.ViewModels;

namespace Agent.Api.Services
{
   
    // Sends outputs to Data Egress exactly as before: one AddNewDataEgress call with the file names and bucket. Data Egress then calls back on /api/Submission/EgressResults with the decisions.
   
    public class DataEgressProvider : IEgressProvider
    {
        private readonly ISubmissionHelper _subHelper;

        public DataEgressProvider(ISubmissionHelper subHelper)
        {
            _subHelper = subHelper;
        }

        public Task<EgressSendResult> SendForReviewAsync(EgressReviewRequest request)
        {
            var result = _subHelper.FilesReadyForReview(new ReviewFiles()
            {
                SubId = request.SubId.ToString(),
                Files = request.Files.Select(f => f.Key).ToList(),
                tesId = request.TesId,
                // Passed through unchanged, exactly as CheckTES did before.
                Name = request.TesName!
            }, request.OutputBucket);

            return Task.FromResult(new EgressSendResult { Success = result?.Result ?? false });
        }
    }
}
