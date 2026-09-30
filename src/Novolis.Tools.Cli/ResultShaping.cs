namespace Novolis.Tools.Cli;

/// <summary>Applies row limits and timing wrappers around tabular results.</summary>
public static class ResultShaping
{
    /// <summary>Truncates rows according to preferences and stamps elapsed time.</summary>
    public static TabularResult Shape(
        IReadOnlyList<string> columns,
        IReadOnlyList<IReadOnlyList<string>> rows,
        int? recordsAffected,
        string unit,
        ReplPreferences prefs,
        TimeSpan elapsed)
    {
        if (columns.Count == 0)
        {
            return TabularResult.Affected(recordsAffected ?? 0, unit, elapsed);
        }

        var limit = prefs.Limit;
        var truncated = limit > 0 && rows.Count > limit;
        var view = truncated ? rows.Take(limit).ToArray() : rows;
        return TabularResult.Grid(columns, view, unit, truncated, elapsed);
    }
}
