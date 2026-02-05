using FiveSafesTes.Core.Models;
using FiveSafesTes.Core.Models.Enums;


namespace Submission.Api.Services
{
    public class UpdateSubmissionStatus
    {
        public static void UpdateStatusNoSave(FiveSafesTes.Core.Models.Submission sub, StatusType type, string? description)
        {
            sub.HistoricStatuses.Add(new HistoricStatus()
            {
                Start = sub.LastStatusUpdate.ToUniversalTime(),
                End = DateTime.Now.ToUniversalTime(),
                Status = sub.Status,
                StatusDescription = sub.StatusDescription
            });
            if (type == StatusType.Cancelled || type == StatusType.Completed)
            {
                sub.EndTime = DateTime.Now.ToUniversalTime();
            }
            sub.Status = type;
            sub.LastStatusUpdate = DateTime.Now.ToUniversalTime();
            sub.StatusDescription = description;
            if (sub.Parent != null)
            {


                UpdateParentStatusNoSave(sub.Parent);
            }


        }

        public static List<StatusType> SubCompleteTypes =>
            new()
            {

                StatusType.Completed,
                StatusType.Cancelled,
                StatusType.Failed,
                StatusType.PartialResult,
                
            };

        private static void UpdateParentStatusNoSave(FiveSafesTes.Core.Models.Submission parent)
        {
            var parentStatus = StatusType.WaitingForChildSubsToComplete;
            if (parent.Children.All(x => SubCompleteTypes.Contains(x.Status)))
            {
                if (parent.Children.All(x => x.Status == StatusType.Failed))
                {
                    UpdateSubmissionStatus.UpdateStatusNoSave(parent, StatusType.Failed, "");
                }
                else if (parent.Children.All(x => x.Status == StatusType.Completed))
                {
                    UpdateSubmissionStatus.UpdateStatusNoSave(parent, StatusType.Completed, "");
                }
                else if (parent.Children.All(x => x.Status == StatusType.Cancelled))
                {
                    UpdateSubmissionStatus.UpdateStatusNoSave(parent, StatusType.Cancelled, "");
                }
                else
                {
                    UpdateSubmissionStatus.UpdateStatusNoSave(parent, StatusType.PartialResult, "");
                }

                parent.EndTime = DateTime.Now.ToUniversalTime();
            }

            
        }
    }
}
