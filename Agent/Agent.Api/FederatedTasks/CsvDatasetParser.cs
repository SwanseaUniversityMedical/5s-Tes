using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using FiveSafesTes.Core.Models.FederatedTasks;

namespace Agent.Api.FederatedTasks
{
    /// <summary>
    /// Parses a CSV stream (header row required) into a <see cref="DatasetTable"/>.
    /// </summary>
    public static class CsvDatasetParser
    {
        public static DatasetReadResult Parse(Stream csv)
        {
            var config = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = true,
                IgnoreBlankLines = true,
            };

            using var reader = new StreamReader(csv, detectEncodingFromByteOrderMarks: true);
            using var parser = new CsvReader(reader, config);

            if (!parser.Read() || !parser.ReadHeader() || parser.HeaderRecord is not { Length: > 0 } header)
            {
                return DatasetReadResult.Fail(FederatedTaskErrorCodes.DatasetNotFound, "dataset file is empty");
            }

            var rows = new List<IReadOnlyList<string?>>();
            while (parser.Read())
            {
                var row = new string?[header.Length];
                for (var i = 0; i < header.Length; i++)
                {
                    row[i] = parser.TryGetField<string>(i, out var value) ? value : null;
                }
                rows.Add(row);
            }

            return DatasetReadResult.Ok(new DatasetTable(header, rows));
        }
    }
}
