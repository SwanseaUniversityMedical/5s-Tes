namespace FiveSafesTes.Core.Models.Settings
{
   
    public enum EgressProvider
    {
        DataEgress = 0,
        Airlock = 1
    }

    public class EgressSettings
    {
    // Default to DataEgress, as this is the most common provider used in FiveSafesTes.
    public EgressProvider Provider { get; set; } = EgressProvider.DataEgress;
    }
}
