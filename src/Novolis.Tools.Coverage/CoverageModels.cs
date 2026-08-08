namespace Novolis.Tools.Coverage;

/// <summary>Options for org-wide / Platform.slnx coverage collection.</summary>
public sealed class CoverageCollectOptions
{
    /// <summary>Workspace root containing <c>novolis-*</c> checkouts.</summary>
    public required string Root { get; init; }

    /// <summary>Output directory for raw Cobertura, logs, and HTML report.</summary>
    public required string OutputDir { get; init; }

    /// <summary>When true, discover hosts from <see cref="PlatformSlnxPath"/> and use ProjectReference mode.</summary>
    public bool PlatformSlnx { get; init; }

    /// <summary>Path to <c>Novolis.Platform.slnx</c> (optional; auto-resolved when <see cref="PlatformSlnx"/>).</summary>
    public string? PlatformSlnxPath { get; init; }

    /// <summary>Run <c>Generate-Platform-Slnx.ps1</c> before collecting (Platform mode only).</summary>
    public bool RegenerateSlnx { get; init; }

    /// <summary>Build/test configuration (default Debug for speed).</summary>
    public string Configuration { get; init; } = "Debug";

    /// <summary>Max parallel repos (default: ProcessorCount - 1).</summary>
    public int ThrottleLimit { get; init; }

    /// <summary>Skip <c>dotnet build</c> and pass <c>--no-build</c> to test.</summary>
    public bool SkipBuild { get; init; }

    /// <summary>
    /// Fail when aggregate line % is below this value.
    /// Use a negative number to disable. When Platform mode and value is 0, defaults to 95.
    /// </summary>
    public double FailBelow { get; init; }

    /// <summary>Extra repo names to exclude.</summary>
    public IReadOnlyList<string> Exclude { get; init; } = [];

    /// <summary>When non-empty, only these repos run (still respects excludes).</summary>
    public IReadOnlyList<string> Include { get; init; } = [];

    /// <summary>Path to exclude file (default: <c>novolis-governance/scripts/coverage-excludes.txt</c>).</summary>
    public string? ExcludeFile { get; init; }

    /// <summary>When true, copy HTML report assets to <see cref="OutputDir"/> root (index.html).</summary>
    public bool FlattenHtml { get; init; }

    /// <summary>Open the HTML index after generation (Windows).</summary>
    public bool OpenReport { get; init; }

    /// <summary>List selected repos and exit without collecting.</summary>
    public bool ListOnly { get; init; }
}

/// <summary>One discovered test-host repo.</summary>
public sealed class CoverageRepo
{
    /// <summary>Repo folder name (e.g. <c>novolis-math</c>).</summary>
    public required string Name { get; init; }

    /// <summary>Absolute path to the repo checkout.</summary>
    public required string Path { get; init; }

    /// <summary>Optional solution path used for discovery.</summary>
    public string? Solution { get; init; }

    /// <summary>Absolute paths to MTP test host projects.</summary>
    public required IReadOnlyList<string> TestProjects { get; init; }
}

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

/// <summary>Parsed Cobertura root rates.</summary>
public sealed class CoberturaSummary
{
    /// <summary>Line coverage percent (0–100).</summary>
    public double LinePercent { get; init; }

    /// <summary>Branch coverage percent (0–100).</summary>
    public double BranchPercent { get; init; }

    /// <summary>Covered lines.</summary>
    public int LinesCovered { get; init; }

    /// <summary>Coverable lines.</summary>
    public int LinesValid { get; init; }

    /// <summary>Covered branches.</summary>
    public int BranchesCovered { get; init; }

    /// <summary>Coverable branches.</summary>
    public int BranchesValid { get; init; }
}
