namespace Novolis.Tools.Coverage;

/// <summary>Per-repo collection outcome.</summary>
public sealed class CoverageRepoResult
{
    /// <summary>Repo folder name.</summary>
    public required string Repo { get; init; }

    /// <summary><c>ok</c> or <c>fail</c>.</summary>
    public required string Status { get; init; }

    /// <summary>Failure message when <see cref="Status"/> is fail.</summary>
    public string? Error { get; init; }

    /// <summary>Wall seconds for this repo.</summary>
    public double Seconds { get; init; }

    /// <summary>Cobertura files produced.</summary>
    public IReadOnlyList<string> CoberturaFiles { get; init; } = [];

    /// <summary>Parsed test total count (best-effort).</summary>
    public int TestsTotal { get; init; }

    /// <summary>Parsed passed count (best-effort).</summary>
    public int TestsPassed { get; init; }

    /// <summary>Parsed failed count (best-effort).</summary>
    public int TestsFailed { get; init; }

    /// <summary>Line coverage percent when available.</summary>
    public double? LinePercent { get; init; }

    /// <summary>Branch coverage percent when available.</summary>
    public double? BranchPercent { get; init; }

    /// <summary>Covered lines.</summary>
    public int LinesCovered { get; init; }

    /// <summary>Coverable lines.</summary>
    public int LinesValid { get; init; }
}
