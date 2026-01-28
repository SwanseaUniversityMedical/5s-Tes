using Agent.Api.Attributes;
using Agent.Api.Constants;
using Agent.Api.Models;
using Agent.Api.Services;
using EasyNetQ;
using FiveSafesTes.Core.Models;
using FiveSafesTes.Core.Models.APISimpleTypeReturns;
using FiveSafesTes.Core.Models.Enums;
using FiveSafesTes.Core.Models.ViewModels;
using FiveSafesTes.Core.Rabbit;
using FiveSafesTes.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.FeatureManagement;
using Serilog;
using Swashbuckle.AspNetCore.Annotations;

namespace Agent.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SubmissionController : Controller
    {       
        private readonly IBus _rabbit;
        private readonly ISubmissionHelper _subHelper;
        private readonly IMinioSubHelper _minioSubHelper;
        private readonly IMinioTreHelper _minioTreHelper;
        private readonly MinioTRESettings _minioTreSettings;
        private readonly AgentSettings _agentSettings;
        private readonly IFeatureManager _features;

        public SubmissionController(           
            IBus rabbit,
            ISubmissionHelper subHelper,
            IMinioSubHelper minioSubHelper,
            IMinioTreHelper minioTreHelper,
            MinioTRESettings minioTreSettings,
            AgentSettings agentSettings, IFeatureManager features)
        {            
            _rabbit = rabbit;
            _subHelper = subHelper;
            _minioTreHelper = minioTreHelper;
            _minioSubHelper = minioSubHelper;
            _minioTreSettings = minioTreSettings;
            _agentSettings = agentSettings;
            _features = features;
        }


        [Authorize(Roles = "dare-tre-admin")]
        [HttpGet("IsUserApprovedOnProject")]
        public BoolReturn IsUserApprovedOnProject(int projectId, int userId)
        {
            return new BoolReturn()
            {
                Result = _subHelper.IsUserApprovedOnProject(projectId, userId)
            };
        }

        [Authorize(Roles = "dare-tre-admin")]
        [HttpGet]
        [Route("GetWaitingSubmissionsForTre")]
        [ValidateModelState]
        [SwaggerOperation("GetWaitingSubmissionsForTre")]
        [SwaggerResponse(statusCode: 200, type: typeof(List<Submission>), description: "")]
        public virtual IActionResult GetWaitingSubmissionsForTre()
        {
            try
            {
                var result = _subHelper.GetWaitingSubmissionForTre();
                return StatusCode(200, result);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "{Function} Crash", "GetWaitingSubmissionsForTre");
                throw;
            }
        }


        [Authorize(Roles = "dare-hutch-admin,dare-tre-admin")]
        [HttpPost]
        [Route("UpdateStatusForTre")]
        [ValidateModelState]
        [SwaggerOperation("UpdateStatusForTre")]
        [SwaggerResponse(statusCode: 200, type: typeof(APIReturn), description: "")]
        public IActionResult UpdateStatusForTre(
            [FromQuery] string subId,
            [FromQuery] StatusType statusType,
            [FromQuery] string? description)
        {
            var subDetails = new SubmissionDetails()
            {
                StatusType = statusType,
                SubId = subId,
                Description = description
            };           

            try
            {
                APIReturn? result =
                    _subHelper.UpdateStatusForTre(subDetails.SubId, subDetails.StatusType, subDetails.Description);
                if (subDetails.StatusType == StatusType.Failure || subDetails.StatusType == StatusType.Cancelled)
                {
                    _subHelper.CloseSubmissionForTre(subDetails.SubId, subDetails.StatusType, "", "");
                }

                return StatusCode(200, result);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "{Function} Crash", "UpdateStatusForTre");
                throw;
            }
        }

        [Authorize(Roles = "dare-hutch-admin,dare-tre-admin")]
        [HttpGet]
        [Route("GetOutputBucketInfo")]
        [ValidateModelState]
        [SwaggerOperation("GetOutputBucketInfo")]
        [SwaggerResponse(statusCode: 200, type: typeof(OutputBucketInfo), description: "")]
        public IActionResult GetOutputBucketInfo(string subId)
        {
            try
            {
                var outputInfo = _subHelper.GetOutputBucketGuts(subId, true, true);


                return StatusCode(200, outputInfo);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "{Function} Crash", "GetOutputBucketInfo");
                throw;
            }
        }

        public class OutputBucketInfo
        {
            public string Host { get; set; }
            public string SubId { get; set; }
            public string Bucket { get; set; }
            public string Path { get; set; }
            public bool Secure { get; set; }
        }


        [Authorize(Roles = "dare-hutch-admin,dare-tre-admin")]
        [HttpPost]
        [Route("FilesReadyForReview")]
        [ValidateModelState]
        [SwaggerOperation("FilesReadyForReview")]
        [SwaggerResponse(statusCode: 200, type: typeof(BoolReturn), description: "")]
        public IActionResult FilesReadyForReview([FromBody] ReviewFiles review)
        {
            try
            {
                var boolResult = _subHelper.FilesReadyForReview(review);
                return StatusCode(200, boolResult);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "{Function} Crash", "FilesReadyForReview");
                throw;
            }
        }


        [Authorize(Roles = "dare-tre-admin,data-egress-admin")]
        [HttpPost]
        [Route("EgressResults")]
        [ValidateModelState]
        [SwaggerOperation("EgressResults")]
        [SwaggerResponse(statusCode: 200, type: typeof(BoolReturn), description: "")]
        public async Task<IActionResult> EgressResults([FromBody] EgressReview review)
        {
            try
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

                if (await _features.IsEnabledAsync(FeatureFlags.DemoAllInOne))
                {
                    var exch = _rabbit.Advanced.ExchangeDeclare(ExchangeConstants.Tre, "topic");
                    var outcome = new FinalOutcome()
                    {
                        File = review.FileResults.First().FileName,
                        SubId = review.SubId
                    };
                    _rabbit.Advanced.Publish(exch, RoutingConstants.ProcessFinalOutput, false,
                        new Message<FinalOutcome>(outcome));
                }               
                else
                {
                    Log.Information(
                        $"EgressResults with review.OutputBucket > {review.OutputBucket} bucket.Bucket > {bucket.Bucket} ");
                    foreach (var file in review.FileResults)
                    {
                        Log.Information(
                            $"EgressResults with File.Approved > {file.Approved} File.FileName > {file.FileName} ");
                        if (file.Approved)
                        {
                            var source = await _minioTreHelper.GetCopyObject(review.OutputBucket, file.FileName);
                            var resultcopy =
                                await _minioSubHelper.CopyObjectToDestination(bucket.Bucket, file.FileName, source);
                        }
                    }

                    _subHelper.UpdateStatusForTre(review.SubId, StatusType.Completed, "");
                }


                return StatusCode(200, new BoolReturn() { Result = true });
            }
            catch (Exception ex)
            {
                Log.Error(ex, "{Function} Crash", "EgressResults");
                throw;
            }
        }


        [Authorize(Roles = "dare-hutch-admin,dare-tre-admin")]
        [HttpPost]
        [Route("FinalOutcome")]
        [ValidateModelState]
        [SwaggerOperation("FinalOutcome")]
        [SwaggerResponse(statusCode: 200, type: typeof(BoolReturn), description: "")]
        public IActionResult FinalOutcome([FromBody] FinalOutcome outcome)
        {
            try
            {
                var exch = _rabbit.Advanced.ExchangeDeclare(ExchangeConstants.Tre, "topic");

                _rabbit.Advanced.Publish(exch, RoutingConstants.ProcessFinalOutput, false,
                    new Message<FinalOutcome>(outcome));

                var boolresult = new BoolReturn()
                {
                    Result = true
                };
                return StatusCode(200, boolresult);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "{Function} Crash", "FinalOutcomeSubmission");
                throw;
            }
        }       

        [HttpPost("SimulateSubmissionProcessing")]
        public IActionResult SimulateSubmissionProcessing(Submission sub)
        {
            try
            {
                //Update status of submission to "Sending to hutch"
                _subHelper.SimulateSubmissionProcessing(sub);

                return StatusCode(200);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "{Function} Crash", "SimulateSubmissionProcessing");
                throw;
            }
        }
    }
}
