using System.Text;
using System.Text.Json;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace Novolis.Tools.Cli;

/// <summary>Spectre-aware tabular result printer.</summary>
public static class ResultPrinter
{
    /// <summary>Writes a result according to <paramref name="mode"/>.</summary>
    public static void Write(IAnsiConsole console, TabularResult result, OutputMode mode, ReplPreferences prefs)
    {
        ArgumentNullException.ThrowIfNull(console);
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(prefs);

        if (result.Columns.Count == 0)
        {
            if (result.RecordsAffected is int n)
            {
                console.MarkupLine($"[green]{n}[/] {Plural(result.Unit, n)} affected{FormatElapsed(result.Elapsed)}");
            }
            else if (prefs.Timer && result.Elapsed is not null)
            {
                console.MarkupLine($"[grey]ok{FormatElapsed(result.Elapsed)}[/]");
            }

            return;
        }

        switch (mode)
        {
            case OutputMode.Csv:
                console.WriteLine(ToCsv(result));
                break;
            case OutputMode.Json:
                console.WriteLine(ToJsonLines(result));
                break;
            default:
                console.Write(BuildTable(result, prefs));
                console.WriteLine();
                break;
        }

        var footer = new StringBuilder();
        footer.Append($"[grey]{result.Rows.Count} {Plural(result.Unit, result.Rows.Count)}[/]");
        if (result.Truncated)
        {
            footer.Append($" [yellow](truncated at limit {prefs.Limit}; .limit 0 for all)[/]");
        }

        footer.Append(FormatElapsedMarkup(result.Elapsed, prefs.Timer));
        console.MarkupLine(footer.ToString());
    }

    /// <summary>Exports a result to a file (mode inferred from extension when <paramref name="mode"/> is null).</summary>
    public static void Export(string path, TabularResult result, OutputMode? mode = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(result);
        var resolved = mode ?? InferMode(path);
        var text = resolved switch
        {
            OutputMode.Csv => ToCsv(result) + Environment.NewLine,
            OutputMode.Json => ToJsonLines(result) + Environment.NewLine,
            _ => ToPlainTable(result) + Environment.NewLine,
        };
        var full = Path.GetFullPath(path);
        var dir = Path.GetDirectoryName(full);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        File.WriteAllText(full, text);
    }

    /// <summary>Builds a Spectre table.</summary>
    public static Table BuildTable(TabularResult result, ReplPreferences prefs)
    {
        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Grey)
            .ShowRowSeparators();

        foreach (var column in result.Columns)
        {
            table.AddColumn(new TableColumn($"[bold]{Markup.Escape(column)}[/]"));
        }

        foreach (var row in result.Rows)
        {
            var cells = new IRenderable[result.Columns.Count];
            for (var i = 0; i < result.Columns.Count; i++)
            {
                var raw = i < row.Count ? row[i] : string.Empty;
                cells[i] = new Markup(FormatCell(raw, prefs));
            }

            table.AddRow(cells);
        }

        return table;
    }

    /// <summary>CSV text.</summary>
    public static string ToCsv(TabularResult result)
    {
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(',', result.Columns.Select(CsvEscape)));
        foreach (var row in result.Rows)
        {
            var cells = new string[result.Columns.Count];
            for (var i = 0; i < result.Columns.Count; i++)
            {
                cells[i] = CsvEscape(i < row.Count ? row[i] : string.Empty);
            }

            sb.AppendLine(string.Join(',', cells));
        }

        return sb.ToString().TrimEnd();
    }

    /// <summary>NDJSON objects.</summary>
    public static string ToJsonLines(TabularResult result)
    {
        if (result.Columns.Count == 0)
        {
            return result.RecordsAffected is int n
                ? $"{{\"affected\":{n}}}"
                : string.Empty;
        }

        var sb = new StringBuilder();
        foreach (var row in result.Rows)
        {
            var obj = new Dictionary<string, object?>();
            for (var i = 0; i < result.Columns.Count; i++)
            {
                var value = i < row.Count ? row[i] : string.Empty;
                obj[result.Columns[i]] = value is "NULL" or null ? null : value;
            }

            sb.AppendLine(JsonSerializer.Serialize(obj));
        }

        return sb.ToString().TrimEnd();
    }

    private static string ToPlainTable(TabularResult result)
    {
        if (result.Columns.Count == 0)
        {
            return result.RecordsAffected is int n ? $"{n} {Plural(result.Unit, n)} affected" : string.Empty;
        }

        var widths = result.Columns.Select((c, i) =>
            Math.Max(c.Length, result.Rows.Count == 0 ? 0 : result.Rows.Max(r => (i < r.Count ? r[i] : string.Empty).Length))).ToArray();
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(" | ", result.Columns.Select((c, i) => c.PadRight(widths[i]))));
        sb.AppendLine(string.Join("-+-", widths.Select(w => new string('-', w))));
        foreach (var row in result.Rows)
        {
            sb.AppendLine(string.Join(" | ", result.Columns.Select((_, i) => (i < row.Count ? row[i] : string.Empty).PadRight(widths[i]))));
        }

        return sb.ToString().TrimEnd();
    }

    private static OutputMode InferMode(string path)
    {
        var ext = Path.GetExtension(path);
        if (ext.Equals(".csv", StringComparison.OrdinalIgnoreCase))
        {
            return OutputMode.Csv;
        }

        if (ext.Equals(".json", StringComparison.OrdinalIgnoreCase) ||
            ext.Equals(".ndjson", StringComparison.OrdinalIgnoreCase))
        {
            return OutputMode.Json;
        }

        return OutputMode.Table;
    }

    private static string FormatCell(string raw, ReplPreferences prefs)
    {
        if (raw == "NULL" || raw == prefs.NullValue)
        {
            return $"[dim italic]{Markup.Escape(prefs.NullValue)}[/]";
        }

        return Markup.Escape(raw);
    }

    private static string FormatElapsed(TimeSpan? elapsed) =>
        elapsed is null ? string.Empty : $" ({elapsed.Value.TotalMilliseconds:0.#} ms)";

    private static string FormatElapsedMarkup(TimeSpan? elapsed, bool enabled) =>
        enabled && elapsed is not null
            ? $" [grey]· {elapsed.Value.TotalMilliseconds:0.#} ms[/]"
            : string.Empty;

    private static string Plural(string unit, int count) =>
        count == 1 ? unit : unit + "s";

    private static string CsvEscape(string value)
    {
        if (value.Contains('"') || value.Contains(',') || value.Contains('\n') || value.Contains('\r'))
        {
            return "\"" + value.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
        }

        return value;
    }
}
