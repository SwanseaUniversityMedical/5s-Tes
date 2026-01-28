namespace Agent.Api.Models
{
    public class HasuraSettings
    {
        public string HasuraURL { get; set; }
        public string HasuraAdminSecret { get; set; }

        public string EnvironmentVariableForDB { get; set; }

        public string DbName { get; set; }

        public bool IsEnabled { get; set; }
    }
}
