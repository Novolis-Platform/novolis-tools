using System.Text;
using LiteDB;
using Novolis.Storage.LiteDb;

namespace Novolis.Tools.LiteDb;

/// <summary>Tabular projection of a LiteDB shell command.</summary>
public sealed class LiteDbQueryResult
{
    /// <summary>Column names in display order.</summary>
    public required IReadOnlyList<string> Columns { get; init; }

    /// <summary>Row values aligned to <see cref="Columns"/>.</summary>
    public required IReadOnlyList<IReadOnlyList<string>> Rows { get; init; }

    /// <summary>Hint for non-result commands.</summary>
    public int? RecordsAffected { get; init; }

    /// <summary>Formats a fixed-width text table.</summary>
    public string ToTable()
    {
        if (Columns.Count == 0)
        {
            return RecordsAffected is int n ? $"{n} document(s) affected" : string.Empty;
        }

        var widths = Columns.Select((c, i) =>
            Math.Max(c.Length, Rows.Count == 0 ? 0 : Rows.Max(r => r[i].Length))).ToArray();
        var sb = new StringBuilder();
        sb.AppendLine(string.Join('|', Columns.Select((c, i) => " " + c.PadRight(widths[i]) + " ")));
        sb.AppendLine(string.Join('+', widths.Select(w => new string('-', w + 2))));
        foreach (var row in Rows)
        {
            sb.AppendLine(string.Join('|', row.Select((c, i) => " " + c.PadRight(widths[i]) + " ")));
        }

        sb.Append($"{Rows.Count} document(s)");
        return sb.ToString();
    }

    /// <summary>Formats as CSV.</summary>
    public string ToCsv()
    {
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(',', Columns.Select(CsvEscape)));
        foreach (var row in Rows)
        {
            sb.AppendLine(string.Join(',', row.Select(CsvEscape)));
        }

        return sb.ToString().TrimEnd();
    }

    /// <summary>Formats each row as a JSON object (one document per line).</summary>
    public string ToJsonLines()
    {
        if (Columns.Count == 0)
        {
            return RecordsAffected is int n ? $"{{\"affected\":{n}}}" : string.Empty;
        }

        var sb = new StringBuilder();
        foreach (var row in Rows)
        {
            var doc = new BsonDocument();
            for (var i = 0; i < Columns.Count; i++)
            {
                doc[Columns[i]] = row[i] == "NULL" ? BsonValue.Null : new BsonValue(row[i]);
            }

            sb.AppendLine(JsonSerializer.Serialize(doc));
        }

        return sb.ToString().TrimEnd();
    }

    private static string CsvEscape(string value)
    {
        if (value.Contains('"') || value.Contains(',') || value.Contains('\n') || value.Contains('\r'))
        {
            return "\"" + value.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
        }

        return value;
    }
}
