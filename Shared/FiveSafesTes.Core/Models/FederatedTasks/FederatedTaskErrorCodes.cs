namespace FiveSafesTes.Core.Models.FederatedTasks
{
    /// <summary>
    /// Error codes reported back to the orchestrator when a federated task fails.
    /// </summary>
    public static class FederatedTaskErrorCodes
    {
        public const string InvalidTask = "invalid_task";
        public const string InvalidDatasetName = "invalid_dataset_name";
        public const string DatasetNotFound = "dataset_not_found";
        public const string AmbiguousDataset = "ambiguous_dataset";
        public const string UnknownOperation = "unknown_operation";
        public const string InvalidFilter = "invalid_filter";
        public const string ColumnNotFound = "column_not_found";
        public const string NonNumericValue = "non_numeric_value";
        public const string InternalError = "internal_error";
    }
}
