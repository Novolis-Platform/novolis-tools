using System.Text;
using Microsoft.Data.Sqlite;
using Novolis.Storage.Sqlite;
using MsSqliteConnection = Microsoft.Data.Sqlite.SqliteConnection;

namespace Novolis.Tools.Sqlite;

/// <summary>Tabular result of a SQL statement.</summary>
public sealed class SqliteQueryResult
{
    /// <summary>Column names in display order.</summary>
    public required IReadOnlyList<string> Columns { get; init; }

    /// <summary>Row values aligned to <see cref="Columns"/>; nulls become <c>NULL</c>.</summary>
    public required IReadOnlyList<IReadOnlyList<string>> Rows { get; init; }

    /// <summary>Rows affected for non-query statements; null when a result set was returned.</summary>
    public int? RecordsAffected { get; init; }

    /// <summary>Formats a fixed-width text table.</summary>
    public string ToTable()
    {
        if (Columns.Count == 0)
        {
            return RecordsAffected is int n ? $"{n} row(s) affected" : string.Empty;
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

        sb.Append($"{Rows.Count} row(s)");
        return sb.ToString();
    }

    /// <summary>Formats as CSV with RFC4180-ish quoting.</summary>
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

    private static string CsvEscape(string value)
    {
        if (value.Contains('"') || value.Contains(',') || value.Contains('\n') || value.Contains('\r'))
        {
            return "\"" + value.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
        }

        return value;
    }
}
