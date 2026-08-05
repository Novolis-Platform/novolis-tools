namespace Novolis.Tools.Cli;

/// <summary>Process exit codes used by Novolis tools.</summary>
public static class ExitCodes
{
    /// <summary>Success.</summary>
    public const int Ok = 0;

    /// <summary>Command or query failed.</summary>
    public const int Failure = 1;

    /// <summary>Invalid arguments / usage.</summary>
    public const int Usage = 2;

    /// <summary>REPL requests exit (internal).</summary>
    public const int Quit = 99;
}

/// <summary>Result rendering mode for tabular queries.</summary>
public enum OutputMode
{
    /// <summary>Spectre / fixed-width table (default interactive).</summary>
    Table = 0,

    /// <summary>CSV for piping.</summary>
    Csv = 1,

    /// <summary>Newline-delimited JSON objects.</summary>
    Json = 2,
}

/// <summary>Parses and formats <see cref="OutputMode"/> values.</summary>
public static class OutputModes
{
    /// <summary>Tries to parse a mode string.</summary>
    public static bool TryParse(string? text, out OutputMode mode)
    {
        mode = OutputMode.Table;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        switch (text.Trim().ToLowerInvariant())
        {
            case "table":
            case "t":
                mode = OutputMode.Table;
                return true;
            case "csv":
                mode = OutputMode.Csv;
                return true;
            case "json":
            case "ndjson":
                mode = OutputMode.Json;
                return true;
            default:
                return false;
        }
    }

    /// <summary>Formats a mode for display.</summary>
    public static string Format(OutputMode mode) => mode switch
    {
        OutputMode.Csv => "csv",
        OutputMode.Json => "json",
        _ => "table",
    };

    /// <summary>Accepted mode names for help text.</summary>
    public const string HelpList = "table|csv|json";
}

/// <summary>In-memory tabular result shared by DB CLIs.</summary>
/// <param name="Columns">Column headers.</param>
/// <param name="Rows">Cell values (NULL already stringified by sessions).</param>
/// <param name="RecordsAffected">Non-query affected count when there is no grid.</param>
/// <param name="Unit">Singular unit label (<c>row</c>, <c>document</c>).</param>
/// <param name="Truncated">True when a row limit cut the result set.</param>
/// <param name="Elapsed">Optional timing.</param>
public sealed record TabularResult(
    IReadOnlyList<string> Columns,
    IReadOnlyList<IReadOnlyList<string>> Rows,
    int? RecordsAffected = null,
    string Unit = "row",
    bool Truncated = false,
    TimeSpan? Elapsed = null)
{
    /// <summary>Creates an empty / affected-only result.</summary>
    public static TabularResult Affected(int count, string unit = "row", TimeSpan? elapsed = null) =>
        new([], [], count, unit, Truncated: false, elapsed);

    /// <summary>Creates a grid result.</summary>
    public static TabularResult Grid(
        IReadOnlyList<string> columns,
        IReadOnlyList<IReadOnlyList<string>> rows,
        string unit = "row",
        bool truncated = false,
        TimeSpan? elapsed = null) =>
        new(columns, rows, RecordsAffected: null, unit, truncated, elapsed);
}

/// <summary>Mutable REPL preferences shared by DB shells.</summary>
public sealed class ReplPreferences
{
    /// <summary>Output mode.</summary>
    public OutputMode Mode { get; set; } = OutputMode.Table;

    /// <summary>Max rows to display (0 = unlimited).</summary>
    public int Limit { get; set; } = 200;

    /// <summary>Show query timings.</summary>
    public bool Timer { get; set; } = true;

    /// <summary>Null display token.</summary>
    public string NullValue { get; set; } = "NULL";

    /// <summary>Confirm destructive statements (DROP/DELETE/TRUNCATE…).</summary>
    public bool ConfirmDestructive { get; set; } = true;

    /// <summary>Session opened read-only.</summary>
    public bool ReadOnly { get; set; }

    /// <summary>Database display path.</summary>
    public string DatabaseLabel { get; set; } = string.Empty;
}
