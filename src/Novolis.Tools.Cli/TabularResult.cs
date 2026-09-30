namespace Novolis.Tools.Cli;

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
