namespace FiveSafesTes.Core.Models.Settings
{
   
    //The service-account credentials are in AirlockKeyCloakSettings, which come from Vault.
    public class AirlockSettings
    {      
        public string ApiUrl { get; set; } = string.Empty;
      
        public int RetryIntervalMinutes { get; set; } = 5;
       
        public int UploadTimeoutMinutes { get; set; } = 30;
       
        public int MaxSendAttempts { get; set; } = 10;

        public string? Plane { get; set; }
        public string? Product { get; set; }
    }
}
