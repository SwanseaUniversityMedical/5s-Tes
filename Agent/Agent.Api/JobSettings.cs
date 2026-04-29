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
    }
}
