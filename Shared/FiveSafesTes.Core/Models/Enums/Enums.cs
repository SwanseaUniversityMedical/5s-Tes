

using System.ComponentModel.DataAnnotations;
using System.Numerics;
using System.Text.Json.Serialization;
using System.Xml.Linq;

namespace FiveSafesTes.Core.Models.Enums
{
    public enum StatusType
    {
        //Stage 1
        //Parent only
        [Display(Name = "Waiting for Child Subs To Complete")]
        WaitingForChildSubsToComplete = 0,
        //Stage 1
        [Display(Name = "Waiting for Agent To Transfer")]
        WaitingForAgentToTransfer = 1,
        //Stage 2
        [Display(Name = "Transferred To Pod")]
        TransferredToPod = 2,
        //Stage 3
        [Display(Name = "Pod Processing")]
        PodProcessing = 3,
        //Stage 3
        //Green
        [Display(Name = "Pod Processing Complete")]
        PodProcessingComplete = 4,
        //Stage 4
        [Display(Name = "Data Out Approval Begun")]
        DataOutApprovalBegun = 5,
        //Stage 4
        //Red
        [Display(Name = "Data Out Rejected")]
        DataOutApprovalRejected = 6,
        //Stage 4
        //Green
        [Display(Name = "Data Out Approved")]
        DataOutApproved = 7,
        //Stage 1
        //Red
        [Display(Name = "User Not On Project")]
        UserNotOnProject = 8,
        //Stage 2
        //Red
        [Display(Name = "User not authorised for project on TRE")]
        InvalidUser = 9,
        //Stage 2
        //Red
        [Display(Name = "TRE Not Authorised For Project")]
        TRENotAuthorisedForProject = 10,
        //Stage 5
        //Green I think this is our completed enum
        [Display(Name = "Completed")]
        Completed = 11,
        //Stage 1
        //Red
        [Display(Name = "Invalid Submission")]
        InvalidSubmission = 12,
        //Stage 1
        //Red
        [Display(Name = "Cancelling Children")]
        CancellingChildren = 13,
        //Stage 1
        //Red
        [Display(Name = "Request Cancellation")]
        RequestCancellation = 14,
        //Stage 1
        //Red
        [Display(Name = "Cancellation Request Sent")]
        CancellationRequestSent = 15,
        //Stage 5
        //Red
        [Display(Name = "Cancelled")]
        Cancelled = 16,
        //Stage 1
        [Display(Name = "Waiting For Crate Format Check")]
        SubmissionWaitingForCrateFormatCheck = 17,
        //Unused
        [Display(Name = "Validating User")]
        ValidatingUser = 18,
        //Unused
        [Display(Name = "Validating Submission")]
        ValidatingSubmission = 19,
        //Unused
        //Green
        [Display(Name = "Validation Successful")]
        ValidationSuccessful = 20,
        //Stage 2
        [Display(Name = "Agent Transferring To Pod")]
        AgentTransferringToPod = 21,
        //Stage 2
        //Red
        [Display(Name = "Transfer To Pod Failed")]
        TransferToPodFailed = 22,
        //Unused
        [Display(Name = "Tre Rejected Project")]
        TRERejectedProject = 23,
        //Unused
        [Display(Name = "Tre Approved Project")]
        TREApprovedProject = 24,
        //Stage 3
        //Red
        [Display(Name = "Pod Processing Failed")]
        PodProcessingFailed = 25,
        //Stage 1
        //Parent only
        [Display(Name = "Running")]
        Running = 26,
        //Stage 5
        //Red
        [Display(Name = "Failed")]
        Failed = 27,     
        //Stage 3
        [Display(Name = "Waiting for a Crate")]
        WaitingForCrate = 30,
        //Stage 3
        [Display(Name = "Fetching Crate")]
        FetchingCrate = 31,
        //Stage 3
        [Display(Name = "Crate queued")]
        Queued = 32,
        //Stage 3
        [Display(Name = "Validating Crate")]
        ValidatingCrate = 33,
        //Stage 3
        [Display(Name = "Fetching workflow")]
        FetchingWorkflow=34,
        //Stage 3
        [Display(Name = "Preparing workflow")]
        StagingWorkflow=35,
        //Stage 3
        [Display(Name = "Executing workflow")]
        ExecutingWorkflow = 36,
        //Stage 3
        [Display(Name = "Preparing outputs")]
        PreparingOutputs = 37,
        //Stage 3
        [Display(Name = "Requested Egress")]
        DataOutRequested=38,
        //Stage 3
        [Display(Name = "Waiting for Egress results")]
        TransferredForDataOut = 39,
        //Stage 3
        [Display(Name = "Finalising approved results")]
        PackagingApprovedResults=40,
        //Stage 3
        //Green
        [Display(Name = "Completed")]
        Complete=41,
        //Stage 3
        //Red
        [Display(Name = "Failed")]
        Failure = 42,
        //Stage 1
        [Display(Name = "Submission has been received")]
        SubmissionReceived = 43,
        //Stage 1
        //Green
        [Display(Name = "Crate Validated")]
        SubmissionCrateValidated = 44,
        //Stage 1
        //Red
        [Display(Name = "Crate Failed Validation")]
        SubmissionCrateValidationFailed = 45,
        //Stage 2
        //Green
        [Display(Name = "Crate Validated")]
        TreCrateValidated = 46,
        //Stage 2
        //Red
        [Display(Name = "Crate Failed Validation")]
        TreCrateValidationFailed = 47,
        //Stage 2
        [Display(Name = "Waiting For Crate Format Check")]
        TreWaitingForCrateFormatCheck = 48,
        //Stage 5
        //Green
        //Parent Only
        [Display(Name = "Complete but not all TREs returned a result")]
        PartialResult = 49,


    }

    public enum Decision
    {
        Undecided = 0,
        Approved = 1,
        Rejected = 2
    }

    public enum FileStatus
    {
        Undecided = 0,
        Approved = 1,
        Rejected = 2
    }

    

    public enum EgressStatus
    {
        [Display(Name = "Not completed")]
        NotCompleted = 0,
        [Display(Name = "Fully Approved")]
        FullyApproved = 1,
        [Display(Name = "Fully Rejected")]
        FullyRejected = 2,
        [Display(Name = "Partially Approved")]
        PartiallyApproved = 3
    }

    public class EnumHelper
    {
        public static List<StatusType> GetHutchAllowedStatusUpdates()
        {
            return new List<StatusType>()
            {
                StatusType.WaitingForCrate,
                StatusType.FetchingCrate,
                StatusType.Queued,
                StatusType.ValidatingCrate,
                StatusType.FetchingWorkflow,
                StatusType.StagingWorkflow,
                StatusType.ExecutingWorkflow,
                StatusType.PreparingOutputs,
                StatusType.DataOutRequested,
                StatusType.TransferredForDataOut,
                StatusType.PackagingApprovedResults,
                StatusType.Complete,
                StatusType.Failure
            };
        }
    }


}
