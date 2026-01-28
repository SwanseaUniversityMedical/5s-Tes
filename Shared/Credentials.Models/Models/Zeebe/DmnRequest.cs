using System.Collections.Generic;

namespace Credentials.Models.Models.Zeebe
{
    public class DmnRequest
    {
        public string DecisionId { get; set; }

        public Dictionary<string, object> Variables { get; set; } = new();
    }
}
