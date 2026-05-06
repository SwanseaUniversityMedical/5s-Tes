using FiveSafesTes.Core.Models.Enums;

namespace Agent.Api.Models
{

  public class Status
  {
    public int Id { get; set; }
    public string Product { get; set; }
    public HealthStatus HealthStatus { get; set; }
    public string Reason { get; set; }
    public DateTime DateTime { get; set; } = DateTime.UtcNow;
  }

}
