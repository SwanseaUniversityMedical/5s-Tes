using System.Text.RegularExpressions;
using Agent.Api.Models;
using FiveSafesTes.Core.Models.Tes;

namespace Agent.Api.Services
{
    public interface ICodePlaceholderScanner
    {
        /// <summary>
        /// Finds every {{code}} and {{@code}} placeholder in the message, wherever it appears.
        /// Does not modify the message.
        /// </summary>
        IReadOnlyList<CodePlaceholder> Scan(TesTask task);

        /// <summary>
        /// Replaces every placeholder with its resolved value, leaving the value blank where
        /// none was supplied. A quickcode outside a container name field is left untouched and
        /// reported instead. Modifies the message in place.
        /// </summary>
        CodeSubstitutionResult Substitute(TesTask task, IReadOnlyDictionary<string, string> resolvedValues);
    }

    /// <summary>
    /// Scans a TES message for code placeholders and substitutes resolved values back into it.
    /// Both passes walk the deserialised object graph rather than the serialised JSON, so a
    /// resolved value containing a quote or a backslash cannot corrupt the message.
    /// </summary>
    public class CodePlaceholderScanner : ICodePlaceholderScanner
    {
        // Matches {{ anything-without-braces }}. Deliberately permissive: a placeholder we fail
        // to recognise would be sent to the container as a literal, whereas one we recognise but
        // cannot resolve fails the job loudly.
        private static readonly Regex PlaceholderPattern =
            new(@"\{\{([^{}]+)\}\}", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        // Returns the replacement for a string, or null to leave it unchanged.
        private delegate string? StringVisitor(string location, bool isContainerImage, string value);

        private readonly ILogger<CodePlaceholderScanner> _logger;

        public CodePlaceholderScanner(ILogger<CodePlaceholderScanner> logger)
        {
            _logger = logger;
        }

        public IReadOnlyList<CodePlaceholder> Scan(TesTask task)
        {
            var found = new List<CodePlaceholder>();

            VisitStrings(task, (location, isContainerImage, value) =>
            {
                foreach (Match match in PlaceholderPattern.Matches(value))
                {
                    var code = NormaliseCode(match.Groups[1].Value);
                    if (code.Length == 0)
                    {
                        continue;
                    }

                    found.Add(new CodePlaceholder(code, match.Value, location, isContainerImage));
                }

                return null;
            });

            _logger.LogInformation(
                "Found {Count} code placeholder(s) ({Distinct} distinct) in TES message {TaskId}",
                found.Count, DistinctCodes(found).Count, task?.Id);

            return found;
        }

        public CodeSubstitutionResult Substitute(TesTask task, IReadOnlyDictionary<string, string> resolvedValues)
        {
            // Codes are matched case-insensitively, so accept a lookup built either way.
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (resolvedValues != null)
            {
                foreach (var pair in resolvedValues)
                {
                    values[pair.Key] = pair.Value;
                }
            }

            var substituted = new List<CodePlaceholder>();
            var unresolved = new List<string>();
            var misplaced = new List<CodePlaceholder>();

            VisitStrings(task, (location, isContainerImage, value) =>
            {
                var replaced = PlaceholderPattern.Replace(value, match =>
                {
                    var code = NormaliseCode(match.Groups[1].Value);
                    var placeholder = new CodePlaceholder(code, match.Value, location, isContainerImage);
                    substituted.Add(placeholder);

                    // A quickcode is only substituted in a container name field. Anywhere
                    // else it is left exactly as it was and fails the whole job, so a malformed
                    // message is never silently dispatched with a half-substituted command.
                    if (placeholder.IsQuickcode && !isContainerImage)
                    {
                        misplaced.Add(placeholder);
                        return match.Value;
                    }

                    if (code.Length > 0
                        && values.TryGetValue(code, out var resolved)
                        && !string.IsNullOrEmpty(resolved))
                    {
                        return resolved;
                    }

                    // leave the substituted value blank. The caller fails the whole job.
                    if (!unresolved.Contains(code, StringComparer.Ordinal))
                    {
                        unresolved.Add(code);
                    }

                    return string.Empty;
                });

                return string.Equals(replaced, value, StringComparison.Ordinal) ? null : replaced;
            });

            // Codes and locations are names, not values, so they are safe to log.
            // Resolved values are not, and are never logged.
            if (misplaced.Count > 0)
            {
                _logger.LogError(
                    "TES message {TaskId} has {Count} quickcode(s) outside a container name field: {Occurrences}",
                    task?.Id, misplaced.Count,
                    string.Join(", ", misplaced.Select(p => $"{p.RawText} at {p.Location}")));
            }

            if (unresolved.Count > 0)
            {
                _logger.LogError(
                    "TES message {TaskId} has {Count} unresolved code(s): {Codes}",
                    task?.Id, unresolved.Count, string.Join(", ", unresolved));
            }

            return new CodeSubstitutionResult(substituted, unresolved, misplaced);
        }

        /// <summary>
        /// The distinct codes across a set of occurrences. The DMN is queried once per distinct
        /// code, not once per occurrence.
        /// </summary>
        public static IReadOnlyList<string> DistinctCodes(IEnumerable<CodePlaceholder> placeholders)
        {
            return placeholders
                .Select(placeholder => placeholder.Code)
                .Distinct(StringComparer.Ordinal)
                .ToList();
        }

        /// <summary>
        /// The quickcode occurrences that are not in a container name field. Lets a caller
        /// reject a malformed message before doing any lookup work.
        /// </summary>
        public static IReadOnlyList<CodePlaceholder> MisplacedQuickcodes(IEnumerable<CodePlaceholder> placeholders)
        {
            return placeholders
                .Where(placeholder => placeholder.IsQuickcode && !placeholder.IsContainerImage)
                .ToList();
        }

        /// <summary>
        /// Codes are lower-cased before lookup, so every case variant resolves to the same value.
        /// </summary>
        private static string NormaliseCode(string raw)
        {
            return raw.Trim().ToLowerInvariant();
        }

        /// <summary>
        /// Walks every string in the message that a placeholder may appear in. Skips the fields
        /// the system owns: id, state, file types, logs and creation time.
        /// </summary>
        private static void VisitStrings(TesTask? task, StringVisitor visit)
        {
            if (task is null)
            {
                return;
            }

            VisitScalar("name", false, () => task.Name, value => task.Name = value, visit);
            VisitScalar("description", false, () => task.Description, value => task.Description = value, visit);
            VisitList("volumes", task.Volumes, visit);
            VisitDictionary("tags", task.Tags, rebuilt => task.Tags = rebuilt, visit);

            if (task.Resources is not null)
            {
                VisitList("resources.zones", task.Resources.Zones, visit);
                VisitDictionary("resources.backend_parameters", task.Resources.BackendParameters,
                    rebuilt => task.Resources.BackendParameters = rebuilt, visit);
            }

            if (task.Executors != null)
            {
                for (var i = 0; i < task.Executors.Count; i++)
                {
                    var executor = task.Executors[i];
                    if (executor is null)
                    {
                        continue;
                    }

                    var prefix = $"executors[{i}]";

                    // The only container name field in the TES schema, and so the only place a
                    // quickcode is allowed.
                    VisitScalar($"{prefix}.image", true, () => executor.Image, value => executor.Image = value, visit);

                    VisitList($"{prefix}.command", executor.Command, visit);
                    VisitScalar($"{prefix}.workdir", false, () => executor.Workdir, value => executor.Workdir = value, visit);
                    VisitScalar($"{prefix}.stdin", false, () => executor.Stdin, value => executor.Stdin = value, visit);
                    VisitScalar($"{prefix}.stdout", false, () => executor.Stdout, value => executor.Stdout = value, visit);
                    VisitScalar($"{prefix}.stderr", false, () => executor.Stderr, value => executor.Stderr = value, visit);
                    VisitDictionary($"{prefix}.env", executor.Env, rebuilt => executor.Env = rebuilt, visit);
                }
            }

            if (task.Inputs != null)
            {
                for (var i = 0; i < task.Inputs.Count; i++)
                {
                    var input = task.Inputs[i];
                    if (input is null)
                    {
                        continue;
                    }

                    var prefix = $"inputs[{i}]";
                    VisitScalar($"{prefix}.name", false, () => input.Name, value => input.Name = value, visit);
                    VisitScalar($"{prefix}.description", false, () => input.Description, value => input.Description = value, visit);
                    VisitScalar($"{prefix}.url", false, () => input.Url, value => input.Url = value, visit);
                    VisitScalar($"{prefix}.path", false, () => input.Path, value => input.Path = value, visit);
                    VisitScalar($"{prefix}.content", false, () => input.Content, value => input.Content = value, visit);
                }
            }

            if (task.Outputs != null)
            {
                for (var i = 0; i < task.Outputs.Count; i++)
                {
                    var output = task.Outputs[i];
                    if (output is null)
                    {
                        continue;
                    }

                    var prefix = $"outputs[{i}]";
                    VisitScalar($"{prefix}.name", false, () => output.Name, value => output.Name = value, visit);
                    VisitScalar($"{prefix}.description", false, () => output.Description, value => output.Description = value, visit);
                    VisitScalar($"{prefix}.url", false, () => output.Url, value => output.Url = value, visit);
                    VisitScalar($"{prefix}.path", false, () => output.Path, value => output.Path = value, visit);
                }
            }
        }

        private static void VisitScalar(string location, bool isContainerImage, Func<string?> get,
            Action<string> set, StringVisitor visit)
        {
            var value = get();
            if (string.IsNullOrEmpty(value))
            {
                return;
            }

            var replacement = visit(location, isContainerImage, value);
            if (replacement != null)
            {
                set(replacement);
            }
        }

        private static void VisitList(string location, List<string>? values, StringVisitor visit)
        {
            if (values == null)
            {
                return;
            }

            for (var i = 0; i < values.Count; i++)
            {
                var value = values[i];
                if (string.IsNullOrEmpty(value))
                {
                    continue;
                }

                var replacement = visit($"{location}[{i}]", false, value);
                if (replacement != null)
                {
                    values[i] = replacement;
                }
            }
        }

        /// <summary>
        /// Visits both keys and values. A placeholder in a key means the dictionary has to be
        /// rebuilt rather than edited in place.
        /// </summary>
        private static void VisitDictionary(string location, Dictionary<string, string>? values,
            Action<Dictionary<string, string>> set, StringVisitor visit)
        {
            if (values == null || values.Count == 0)
            {
                return;
            }

            var rebuilt = new Dictionary<string, string>(values.Count);
            var changed = false;

            foreach (var pair in values)
            {
                var key = pair.Key;
                var value = pair.Value;

                if (!string.IsNullOrEmpty(key))
                {
                    var replacement = visit($"{location}.keys[{pair.Key}]", false, key);
                    if (replacement != null)
                    {
                        key = replacement;
                        changed = true;
                    }
                }

                if (!string.IsNullOrEmpty(value))
                {
                    var replacement = visit($"{location}[{pair.Key}]", false, value);
                    if (replacement != null)
                    {
                        value = replacement;
                        changed = true;
                    }
                }

                // Last one wins if two keys substitute down to the same name.
                rebuilt[key] = value;
            }

            if (changed)
            {
                set(rebuilt);
            }
        }
    }
}
