using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FiveSafesTes.Core.Models
{
    public class EmailSettings
    {
        public string Host { get; set; }
        public int Port { get; set; }
        public bool EnableSSL { get; set; }
        public string FromAddress { get; set; }
        public string FromDisplayName { get; set; }

        public HashSet<string> EmailsToIgnor { get; set; }

        public string EmailOverride  { get; set; }

        public bool Enabled { get; set; }
    }
}
