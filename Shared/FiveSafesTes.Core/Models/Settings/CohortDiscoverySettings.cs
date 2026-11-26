using System;
using System.Collections.Generic;
using System.Text;

namespace FiveSafesTes.Core.Models.Settings;

public class CohortDiscoverySettings
{
    public bool EnableCohortDiscoveryTool { get; set; } = false;
    public string BaseUrl { get; set; } = "http://localhost:8100";
    public string AdminEmail { get; set; } = string.Empty;
    public string AdminPassword { get; set; } = string.Empty;
    public int TimeoutSeconds { get; set; } = 30;
    public string DefaultQueryContextType { get; set; } = "bunny";
}
