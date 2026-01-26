using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FiveSafesTes.Core.Models.ViewModels
{
    public class AuthorizationResult
    {
        public bool IsAuthorized { get; set; }
        public string? allow { get; set; }
        public string? any_invalid_tre { get; set; }
        public string? any_valid_users { get; set; }
        public string? project_allow { get; set; }
        public string? user_in_tre { get; set; }
    }
}
