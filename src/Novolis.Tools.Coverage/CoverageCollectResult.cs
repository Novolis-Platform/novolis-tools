namespace Novolis.Tools.Coverage;

/// <summary>Aggregate collection result.</summary>
public sealed class CoverageCollectResult
{
    /// <summary>Output directory used.</summary>
    public required string OutputDir { get; init; }

    /// <summary>Path to HTML index when generated.</summary>
    public required string? HtmlIndexPath { get; init; }

    /// <summary>Path to SUMMARY.md.</summary>
    public required string SummaryMarkdownPath { get; init; }

    /// <summary>Aggregate line percent.</summary>
    public double? AggregateLinePercent { get; init; }

    /// <summary>Aggregate branch percent.</summary>
    public double? AggregateBranchPercent { get; init; }

    /// <summary>Total wall seconds.</summary>
    public double DurationSeconds { get; init; }

    /// <summary>Per-repo results.</summary>
    public required IReadOnlyList<CoverageRepoResult> Repos { get; init; }

    /// <summary>True when FailBelow gate tripped.</summary>
    public bool GateFailed { get; init; }

    /// <summary>Gate failure message.</summary>
    public string? GateMessage { get; init; }
}
