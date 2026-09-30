namespace Novolis.Tools.Cli;

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
