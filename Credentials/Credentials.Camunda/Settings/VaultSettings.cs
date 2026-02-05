using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Credentials.Camunda.Settings
{
    public class VaultSettings
    {
        public string BaseUrl { get; set; } = "http://localhost:8200";
        public string Token { get; set; } = string.Empty;
        public int TimeoutSeconds { get; set; } = 30;
        public string SecretEngine { get; set; } = "secret"; // KV v2 engine name
        public bool EnableRetry { get; set; } = true;
        public int MaxRetryAttempts { get; set; } = 3;
    }
}
