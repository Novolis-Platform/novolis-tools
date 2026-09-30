namespace Novolis.Tools.Cli;

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
