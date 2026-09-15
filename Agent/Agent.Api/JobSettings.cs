namespace Agent.Api
{

    public class JobSettings
    {

        public int syncSchedule { get; set; }
        public int scanSchedule { get; set; }
        public int healthCheckSchedule { get; set; }
        public string SyncJobName { get; set; }
        public string ScanJobName { get; set; }
        public string HealthCheckJobName { get; set; }
        public int DaysBeforeHealthLogDeletion { get; set; } = 30;

        // Bucket cleanup for expired projects. 0 disables the job; a non-zero value is the
        // hour of day (1-23, UTC) it runs daily, e.g. 2 = 02:00.
        public int bucketCleanupSchedule { get; set; }
        public string BucketCleanupJobName { get; set; }
        public int DaysAfterExpiryBeforeBucketDeletion { get; set; } = 90;
    }
}
