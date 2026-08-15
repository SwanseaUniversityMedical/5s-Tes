namespace FiveSafesTes.Core.Utilities
{
    public static class SqlProvenanceHelper
    {
        public static string NormalizeSql(string sql)
        {
            if (string.IsNullOrWhiteSpace(sql))
                return "";

            var normalized = sql.Trim();
            normalized = System.Text.RegularExpressions.Regex.Replace(normalized, @"\s+", " ");
            normalized = System.Text.RegularExpressions.Regex.Replace(normalized, @"'([^']|'')*'", "?");
            normalized = System.Text.RegularExpressions.Regex.Replace(normalized, @"\b\d+(?:\.\d+)?\b", "?");
            normalized = System.Text.RegularExpressions.Regex.Replace(normalized, @"\b(?:true|false|null)\b", "?");

            return normalized;
        }

        public static string ExtractTableNames(string sql)
        {
            if (string.IsNullOrWhiteSpace(sql))
                return string.Empty;

            var matches = System.Text.RegularExpressions.Regex.Matches(
                sql,
                @"\b(?:from|join|update|into|delete\s+from|insert\s+into|truncate\s+table)\s+([A-Za-z_][A-Za-z0-9_]*)",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant);

            var tables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (System.Text.RegularExpressions.Match match in matches)
            {
                if (match.Groups.Count > 2 && !string.IsNullOrWhiteSpace(match.Groups[2].Value))
                {
                    tables.Add(match.Groups[2].Value.Trim('[', ']', '"'));
                }
            }

            return string.Join(",", tables);
        }
    }
}
