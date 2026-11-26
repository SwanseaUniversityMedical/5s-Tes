using System;
using System.Collections.Generic;
using System.Text;

namespace FiveSafesTes.Core.Models;

public class DirectorGovernanceConfig
{
    public List<GovernanceTre> ConsortiumTres { get; set; }
    public List<GovernanceProduct> EnabledProducts { get; set; }
    public List<GovernanceUser> PresentUsers { get; set; }
}

public class GovernanceTre
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string Code { get; set; }
    public string OtherInformation { get; set; }
}

public class GovernanceProduct
{
    public int Id { get; set; }
    public GovernanceProductMetadata ProductMetadata { get; set; }
    public List<int> AssignedTres { get; set; }
}

public class GovernanceProductMetadata
{
    public string Name { get; set; }
    public string Version { get; set; }
}

public class GovernanceUser
{
    public int Id { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public string Email { get; set; }
    public string Role { get; set; }
    public int RepresentedTreId { get; set; }
}
