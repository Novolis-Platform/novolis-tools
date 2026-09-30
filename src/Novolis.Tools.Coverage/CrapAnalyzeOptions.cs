using System.Collections.Concurrent;
using System.Globalization;
using System.Text;

namespace Novolis.Tools.Coverage;

/// <summary>Options for platform-scoped CRAP analysis.</summary>
public sealed class CrapAnalyzeOptions
{
    /// <summary>Novolis workspace root.</summary>
    public required string Root { get; init; }

    /// <summary>Path to <c>Novolis.Platform.slnx</c> (resolved when null).</summary>
    public string? PlatformSlnxPath { get; init; }

    /// <summary>Coverage output root (default <c>&lt;root&gt;/coverage</c>).</summary>
    public string? CoverageDir { get; init; }

    /// <summary>Explicit Cobertura file(s); when set, skips platform discovery.</summary>
    public IReadOnlyList<string> CoberturaPaths { get; init; } = [];

    /// <summary>Flag threshold (default 30).</summary>
    public double Threshold { get; init; } = CrapScore.DefaultThreshold;

    /// <summary>Max parallel Cobertura parses (0 → ProcessorCount − 1).</summary>
    public int MaxDegreeOfParallelism { get; init; }

    /// <summary>Extra repo excludes (with default exclude file).</summary>
    public IReadOnlyList<string> Exclude { get; init; } = [];

    /// <summary>Optional include filter.</summary>
    public IReadOnlyList<string> Include { get; init; } = [];

    /// <summary>Exclude list file (default governance coverage-excludes).</summary>
    public string? ExcludeFile { get; init; }
}
