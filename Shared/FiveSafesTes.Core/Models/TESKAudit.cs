namespace FiveSafesTes.Core.Models;

public class TeskAudit
{
    public int id { get; set; }
    public string message { get; set; }
    public string teskid { get; set; }
    public string subid { get; set; }
    public string dated { get; set; } = DateTime.Now.ToString("yyyyMMddHHmmss");
}