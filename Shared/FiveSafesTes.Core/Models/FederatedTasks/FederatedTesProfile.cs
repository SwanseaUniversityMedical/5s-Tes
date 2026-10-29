using FiveSafesTes.Core.Models.Tes;

namespace FiveSafesTes.Core.Models.FederatedTasks
{
    /// <summary>
    /// The restricted TES "federated profile": validates a <see cref="TesTask"/> and converts it to a
    /// <see cref="FederatedTaskSpec"/> (and back).
    /// </summary>
    public static class FederatedTesProfile
    {
        public const string ExecutorImage = "5s-tes/federated-ops:1";
        public const string DatasetUrlScheme = "dataset://";

        private const string TresTag = "tres";
        private const string StudyIdTag = "study_id";
        private const string ColumnArg = "--column";
        private const string FilterArg = "--filter";

        public static readonly IReadOnlySet<string> Operations =
            new HashSet<string>(StringComparer.Ordinal) { "count", "mean" };

        public static readonly IReadOnlySet<string> FilterOperators =
            new HashSet<string>(StringComparer.Ordinal) { "eq", "gt", "lt", "gte", "lte" };

        /// <summary>
        /// Applies the profile rules in order (executors, command, inputs, outputs, tags); the first failure wins.
        /// </summary>
        public static FederatedTaskParseResult Parse(TesTask task)
        {
            // Executors
            if (task.Executors == null || task.Executors.Count != 1)
                return Fail(FederatedTaskErrorCodes.InvalidTask, "task must have exactly one executor");
            var executor = task.Executors[0];
            if (executor == null)
                return Fail(FederatedTaskErrorCodes.InvalidTask, "executor must not be null");
            if (executor.Image != ExecutorImage)
                return Fail(FederatedTaskErrorCodes.InvalidTask, $"executor image must be {ExecutorImage}");

            // Command: operation, then arguments
            var command = executor.Command;
            if (command == null || command.Count == 0)
                return Fail(FederatedTaskErrorCodes.InvalidTask, "executor command is required");
            var operation = command[0];
            if (!Operations.Contains(operation))
                return Fail(FederatedTaskErrorCodes.UnknownOperation, $"unknown operation '{operation}'");

            string? column = null;
            var filters = new List<TaskFilter>();
            for (var i = 1; i < command.Count; i++)
            {
                var arg = command[i];
                switch (arg)
                {
                    case ColumnArg:
                        if (column != null)
                            return Fail(FederatedTaskErrorCodes.InvalidTask, $"{ColumnArg} may be given only once");
                        if (i + 1 >= command.Count || string.IsNullOrWhiteSpace(command[i + 1]))
                            return Fail(FederatedTaskErrorCodes.InvalidTask, $"{ColumnArg} requires a value");
                        column = command[++i];
                        break;
                    case FilterArg:
                        if (i + 1 >= command.Count || string.IsNullOrWhiteSpace(command[i + 1]))
                            return Fail(FederatedTaskErrorCodes.InvalidFilter, $"{FilterArg} requires a value");
                        var filter = ParseFilter(command[++i], out var filterError);
                        if (filter == null)
                            return Fail(FederatedTaskErrorCodes.InvalidFilter, filterError!);
                        filters.Add(filter);
                        break;
                    default:
                        return Fail(FederatedTaskErrorCodes.InvalidTask, $"unknown argument '{arg}'");
                }
            }

            if (operation == "mean" && column == null)
                return Fail(FederatedTaskErrorCodes.InvalidTask, $"operation 'mean' requires {ColumnArg}");

            // Inputs
            if (task.Inputs == null || task.Inputs.Count != 1)
                return Fail(FederatedTaskErrorCodes.InvalidTask, "task must have exactly one input");
            if (task.Inputs[0] == null)
                return Fail(FederatedTaskErrorCodes.InvalidTask, "input must not be null");
            var url = task.Inputs[0].Url;
            if (url == null || !url.StartsWith(DatasetUrlScheme, StringComparison.Ordinal))
                return Fail(FederatedTaskErrorCodes.InvalidTask, $"input url must start with {DatasetUrlScheme}");
            var dataset = url.Substring(DatasetUrlScheme.Length);
            if (!FederatedTaskNames.IsValidDatasetName(dataset))
                return Fail(FederatedTaskErrorCodes.InvalidDatasetName, "invalid dataset name");

            // Outputs
            if (task.Outputs != null && task.Outputs.Count > 0)
                return Fail(FederatedTaskErrorCodes.InvalidTask, "task must not declare outputs");

            // Tags
            var tags = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var tag in task.Tags ?? [])
            {
                if (!tags.TryAdd(tag.Key, tag.Value))
                    return Fail(FederatedTaskErrorCodes.InvalidTask, "duplicate tag key");
            }

            // Exactly one TRE: non-empty after trimming, no '|' separator. Existence is checked later.
            if (!tags.TryGetValue(TresTag, out var tre) || string.IsNullOrWhiteSpace(tre) || tre.Contains('|'))
                return Fail(FederatedTaskErrorCodes.InvalidTask, $"tag '{TresTag}' must name exactly one TRE");
            if (!tags.TryGetValue(StudyIdTag, out var studyId) || string.IsNullOrWhiteSpace(studyId))
                return Fail(FederatedTaskErrorCodes.InvalidTask, $"tag '{StudyIdTag}' is required");

            var spec = new FederatedTaskSpec(operation, dataset, column, filters, studyId, tre.Trim().ToLowerInvariant());
            return new FederatedTaskParseResult(spec, null, null);
        }

        /// <summary>
        /// Builds the TES task for a spec; the inverse of <see cref="Parse"/>.
        /// </summary>
        public static TesTask Build(FederatedTaskSpec spec)
        {
            var command = new List<string> { spec.Operation };
            if (spec.Column != null)
            {
                command.Add(ColumnArg);
                command.Add(spec.Column);
            }

            foreach (var filter in spec.Filters)
            {
                command.Add(FilterArg);
                command.Add($"{filter.Column}:{filter.Op}:{filter.Value}");
            }

            return new TesTask
            {
                Executors = [new TesExecutor { Image = ExecutorImage, Command = command }],
                Inputs =
                [
                    new TesInput
                    {
                        Url = DatasetUrlScheme + spec.Dataset,
                        Path = "/data/" + spec.Dataset,
                        Type = TesFileType.DIRECTORYEnum
                    }
                ],
                Outputs = [],
                Tags = new Dictionary<string, string>
                {
                    [TresTag] = spec.TreName,
                    [StudyIdTag] = spec.StudyId
                }
            };
        }

        private static TaskFilter? ParseFilter(string text, out string? error)
        {
            // column:op:value; the value may itself contain ':'.
            var parts = text.Split(':', 3);
            if (parts.Length != 3 || string.IsNullOrEmpty(parts[0]))
            {
                error = "filter must be column:op:value";
                return null;
            }

            if (!FilterOperators.Contains(parts[1]))
            {
                error = $"unknown filter operator '{parts[1]}'";
                return null;
            }

            error = null;
            return new TaskFilter(parts[0], parts[1], parts[2]);
        }

        private static FederatedTaskParseResult Fail(string code, string message) =>
            new(null, code, message);
    }
}
