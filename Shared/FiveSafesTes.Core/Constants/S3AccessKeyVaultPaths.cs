namespace FiveSafesTes.Core.Constants
{
    public static class S3AccessKeyVaultPaths
    {
        public const string Prefix = "S3Accesskeys";

        public static string ForProject(int projectId) => $"{Prefix}/{projectId}";
    }
}
