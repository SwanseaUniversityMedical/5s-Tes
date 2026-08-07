namespace Agent.Api.Helpers;

public static class UrlHelper
{
    /// <summary>
    /// Combines a base address and relative path into a URI, avoiding double-slash issues
    /// when the base address already ends with a trailing slash.
    /// </summary>
    public static Uri Combine(string baseAddress, string relativePath)
    {
        ArgumentException.ThrowIfNullOrEmpty(baseAddress);
        ArgumentNullException.ThrowIfNull(relativePath);

        var baseUri = new Uri(baseAddress.TrimEnd('/') + "/");
        return new Uri(baseUri, relativePath.TrimStart('/'));
    }
}
