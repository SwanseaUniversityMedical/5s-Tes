using FiveSafesTes.Core.Models.Tes;

namespace FiveSafesTes.Core.Services;

/// <summary>
/// Ensures TES tasks include a writable /tmp volume mount for every execution.
/// </summary>
public static class TesVolumeHelper
{
    public const string DefaultTmpVolume = "/tmp";

    /// <summary>
    /// Merges user-provided volumes with the default /tmp mount, preserving order and avoiding duplicates.
    /// </summary>
    public static List<string> EnsureDefaultTmpVolume(IEnumerable<string>? volumes)
    {
        var merged = new List<string> { DefaultTmpVolume };
        var seen = new HashSet<string>(StringComparer.Ordinal) { NormalizeVolumePath(DefaultTmpVolume) };

        if (volumes == null)
        {
            return merged;
        }

        foreach (var volume in volumes)
        {
            if (string.IsNullOrWhiteSpace(volume))
            {
                continue;
            }

            var normalized = NormalizeVolumePath(volume);
            if (seen.Add(normalized))
            {
                merged.Add(volume.Trim());
            }
        }

        return merged;
    }

    /// <summary>
    /// Applies the default /tmp volume to a TES task before submission to a TES backend.
    /// </summary>
    public static void ApplyDefaultTmpVolume(TesTask task)
    {
        ArgumentNullException.ThrowIfNull(task);
        task.Volumes = EnsureDefaultTmpVolume(task.Volumes);
    }

    internal static string NormalizeVolumePath(string path)
    {
        var trimmed = path.Trim();
        if (trimmed == "/")
        {
            return "/";
        }

        return trimmed.TrimEnd('/');
    }
}
