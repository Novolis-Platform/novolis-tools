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

    /// <summary>Regenerate Novolis.Platform.slnx before collecting (Platform mode only).</summary>
    public bool RegenerateSlnx { get; init; }

    /// <summary>Build/test configuration (default Debug for speed).</summary>
    public string Configuration { get; init; } = "Debug";

    /// <summary>Max parallel repos (default: ProcessorCount - 1).</summary>
    public int ThrottleLimit { get; init; }

    /// <summary>Skip <c>dotnet build</c> and pass <c>--no-build</c> to test.</summary>
    public bool SkipBuild { get; init; }

    /// <summary>
    /// Fail when aggregate line OR branch % is below this value.
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
