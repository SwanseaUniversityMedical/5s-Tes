namespace TeleportUserManagement.Models.Settings
{
    public class ActiveDirectorySettings
    {
        public ConnectionOptions Connection { get; set; }
        public BehaviourOptions Behaviour { get; set; }
    }

    public class ConnectionOptions
    {
        public string Domain { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public string Machine { get; set; }
        public bool UseSsl { get; set; } = true;
    }

    public class BehaviourOptions
    {
        public string ShortDomain { get; set; }
    }
}