using FiveSafesTes.Core.Models;
using FiveSafesTes.Core.Models.APISimpleTypeReturns;
using FiveSafesTes.Core.Models.Enums;
using FiveSafesTes.Core.Models.ViewModels;
using FiveSafesTes.Core.Services;
using Serilog;

namespace Agent.Api.Services
{
    public interface IEgressOutcomeService
    {
        /// <summary>
        /// Applies an egress review outcome: updates the submission status and copies the approved
        /// files from the TRE output bucket to the project's submission bucket.
        /// Used by the Data Egress callback (EgressResults) and the Airlock callback (AirlockResults).
        /// </summary>
        Task<BoolReturn> ApplyOutcomeAsync(EgressReview review);
    }

    public class EgressOutcomeService : IEgressOutcomeService
    {
        private readonly ISubmissionHelper _subHelper;
        private readonly IMinioTreHelper _minioTreHelper;
        private readonly IProjectS3SubHelperFactory _projectS3SubHelperFactory;
        private readonly IDareClientWithoutTokenHelper _dareHelper;

        public EgressOutcomeService(ISubmissionHelper subHelper, IMinioTreHelper minioTreHelper,
            IProjectS3SubHelperFactory projectS3SubHelperFactory, IDareClientWithoutTokenHelper dareHelper)
        {
            _subHelper = subHelper;
            _minioTreHelper = minioTreHelper;
            _projectS3SubHelperFactory = projectS3SubHelperFactory;
            _dareHelper = dareHelper;
        }

        public async Task<BoolReturn> ApplyOutcomeAsync(EgressReview review)
        {
            ApprovalType approvalStatus;
            var approvedCount = review.FileResults.Count(x => x.Approved);
            var rejectedCount = review.FileResults.Count(x => !x.Approved);

            if (approvedCount == review.FileResults.Count)
            {
                approvalStatus = ApprovalType.FullyApproved;
            }
            else if (rejectedCount == review.FileResults.Count)
            {
                approvalStatus = ApprovalType.NotApproved;
            }
            else
            {
                approvalStatus = ApprovalType.PartiallyApproved;
            }

            if (approvalStatus != ApprovalType.FullyApproved)
            {
                _subHelper.UpdateStatusForTre(review.SubId.ToString(), StatusType.DataOutApprovalRejected, "");


                var statusResult = _subHelper.CloseSubmissionForTre(review.SubId, StatusType.Failed, "", "");
            }
            else
            {
                _subHelper.UpdateStatusForTre(review.SubId.ToString(), StatusType.DataOutApproved, "");
            }

            var bucket = _subHelper.GetOutputBucketGutsSub(review.SubId, true);

            Log.Information(
              $"EgressResults with review.OutputBucket > {review.OutputBucket} bucket.Bucket > {bucket.Bucket} ");

            var queryParams = new Dictionary<string, string> { ["responseType"] = "summary" };
            var submission = await _dareHelper.CallAPIWithoutModel<Submission.SubmissionDetailsDto>(
                $"/api/Submission/GetASubmission/{review.SubId}",
                queryParams);
            var minioSubHelper = await _projectS3SubHelperFactory.GetProjectS3HelperAsync(submission.Project.Id);

            foreach (var file in review.FileResults)
            {
                Log.Information(
                  $"EgressResults with File.Approved > {file.Approved} File.FileName > {file.FileName} ");
                if (file.Approved)
                {
                    var source = await _minioTreHelper.GetCopyObject(review.OutputBucket, file.FileName);
                    var resultcopy =
                      await minioSubHelper.CopyObjectToDestination(bucket.Bucket, file.FileName, source);
                }
            }

            _subHelper.UpdateStatusForTre(review.SubId, StatusType.Completed, "");

            return new BoolReturn() { Result = true };
        }
    }
}
