namespace Novolis.Tools.Cli;

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
