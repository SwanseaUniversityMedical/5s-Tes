using System.Text.Json;
using System.Text.RegularExpressions;
using Credentials.Models.Models.Zeebe;
using Credentials.Models.Services;
using FiveSafesTes.Core.Constants;

namespace Agent.Api.Services
{
    public interface IImageCodeResolver
    {
        /// <summary>
        /// The image codes written as {{@code}} in an executor's image field. Usually one,
        /// and empty when the researcher gave a literal image name.
        /// </summary>
        IReadOnlyList<string> FindImageCodes(string? image);

        /// <summary>
        /// Asks the image code DMN what an image code resolves to for this project and user.
        /// Returns null when no rule matches, which fails the job rather than running
        /// something unintended.
        /// </summary>
        Task<string?> ResolveImageAsync(string project, string user, string imageCode,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Resolves every image code in one executor's image field and substitutes the
        /// results, reporting any that could not be resolved.
        /// </summary>
        Task<ImageCodeResolution> ResolveExecutorImageAsync(string? image, string project, string user,
            CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// The outcome of resolving one executor's image field.
    /// </summary>
    public class ImageCodeResolution
    {
        public ImageCodeResolution(string? image, IReadOnlyList<string> imageCodes, IReadOnlyList<string> unresolved)
        {
            Image = image;
            ImageCodes = imageCodes;
            Unresolved = unresolved;
        }

        /// <summary>The image field with every resolvable code substituted.</summary>
        public string? Image { get; }

        /// <summary>The codes found, whether or not they resolved. Used to pick this executor's variables.</summary>
        public IReadOnlyList<string> ImageCodes { get; }

        /// <summary>Codes with no matching DMN rule. Any entry here fails the whole job.</summary>
        public IReadOnlyList<string> Unresolved { get; }

        public bool Success => Unresolved.Count == 0;
    }

    /// <summary>
    /// Turns the {{@imageCode}} placeholder in an executor's image field into the container
    /// image to actually run, by asking the image code DMN.
    ///
    /// This is a plain decision lookup rather than a Camunda process: image codes vary per
    /// executor, while the credentials process runs once per submission, so there is nothing
    /// to orchestrate and nothing to store.
    /// </summary>
    public class ImageCodeResolver : IImageCodeResolver
    {
        // {{@code}} - the @ is part of the code, and is what distinguishes an image code
        // from the general {{code}} syntax that is parked.
        private static readonly Regex ImageCodePattern =
            new(@"\{\{\s*(@[^{}\s]+)\s*\}\}", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private readonly IServicedZeebeClient _zeebeClient;
        private readonly ILogger<ImageCodeResolver> _logger;

        public ImageCodeResolver(IServicedZeebeClient zeebeClient, ILogger<ImageCodeResolver> logger)
        {
            _zeebeClient = zeebeClient;
            _logger = logger;
        }

        public IReadOnlyList<string> FindImageCodes(string? image)
        {
            if (string.IsNullOrWhiteSpace(image))
            {
                return Array.Empty<string>();
            }

            return ImageCodePattern.Matches(image)
                .Select(match => match.Groups[1].Value.Trim().ToLowerInvariant())
                .Where(code => code.Length > 1)
                .Distinct(StringComparer.Ordinal)
                .ToList();
        }

        public async Task<string?> ResolveImageAsync(string project, string user, string imageCode,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var response = await _zeebeClient.EvaluateDecisionModelAsync(new DmnRequest
                {
                    DecisionId = DmnFiles.ImageCodesDecisionId,
                    Variables = new Dictionary<string, object>
                    {
                        ["project"] = project,
                        ["user"] = user,
                        ["imageCode"] = imageCode
                    }
                });

                var image = ReadImage(response);

                if (string.IsNullOrWhiteSpace(image))
                {
                    _logger.LogError(
                        "The image code DMN returned no image for {ImageCode} (project {Project}, user {User})",
                        imageCode, project, user);

                    return null;
                }

                _logger.LogInformation("Image code {ImageCode} resolved to {Image}", imageCode, image);
                return image;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception,
                    "Failed to evaluate the image code DMN for {ImageCode} (project {Project})",
                    imageCode, project);

                return null;
            }
        }

        public async Task<ImageCodeResolution> ResolveExecutorImageAsync(string? image, string project, string user,
            CancellationToken cancellationToken = default)
        {
            var imageCodes = FindImageCodes(image);

            if (imageCodes.Count == 0)
            {
                // A literal image name. Nothing to do.
                return new ImageCodeResolution(image, imageCodes, Array.Empty<string>());
            }

            var resolvedByCode = new Dictionary<string, string>(StringComparer.Ordinal);
            var unresolved = new List<string>();

            foreach (var imageCode in imageCodes)
            {
                var resolved = await ResolveImageAsync(project, user, imageCode, cancellationToken);

                if (string.IsNullOrWhiteSpace(resolved))
                {
                    unresolved.Add(imageCode);
                    continue;
                }

                resolvedByCode[imageCode] = resolved;
            }

            // Substituted through the same pattern that found them, so the replacement matches
            // however the researcher cased or spaced the placeholder.
            var substituted = ImageCodePattern.Replace(image!, match =>
            {
                var code = match.Groups[1].Value.Trim().ToLowerInvariant();
                return resolvedByCode.TryGetValue(code, out var value) ? value : match.Value;
            });

            return new ImageCodeResolution(substituted, imageCodes, unresolved);
        }

        /// <summary>
        /// Pulls the image out of the decision result.
        ///
        /// ServicedZeebeClient reshapes what Zeebe returns: a decision with a single output
        /// column gives a bare value, which it wraps as { "result": value }, while a
        /// multi-output decision keeps its real column names. The image code table has one
        /// output today, so "result" is the expected shape, but both are handled so a future
        /// column on that table cannot quietly break resolution.
        /// </summary>
        private static string? ReadImage(DmnResponse response)
        {
            if (response.Result is null || response.Result.Count == 0)
            {
                return null;
            }

            foreach (var key in new[] { "image", "result" })
            {
                if (response.Result.TryGetValue(key, out var value))
                {
                    var text = AsText(value);
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        return text;
                    }
                }
            }

            // Single unnamed value under some other key.
            return response.Result.Count == 1 ? AsText(response.Result.Values.First()) : null;
        }

        private static string? AsText(object? value)
        {
            if (value is null)
            {
                return null;
            }

            // Dictionary<string, object> from System.Text.Json holds JsonElement values.
            if (value is JsonElement element)
            {
                return element.ValueKind switch
                {
                    JsonValueKind.String => element.GetString(),
                    JsonValueKind.Null or JsonValueKind.Undefined => null,
                    _ => element.ToString()
                };
            }

            return value.ToString();
        }
    }
}
