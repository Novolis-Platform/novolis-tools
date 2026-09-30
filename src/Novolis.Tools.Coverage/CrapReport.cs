using System.Collections.Concurrent;
using System.Globalization;
using System.Text;

namespace Novolis.Tools.Coverage;

/// <summary>CRAP analysis over Cobertura method rows (platform-wide or single file).</summary>
public sealed class CrapReport
{
    /// <summary>Primary Cobertura path when a single file was used.</summary>
    public string? SourcePath { get; init; }

    /// <summary>All Cobertura inputs scored (parallel platform fan-in).</summary>
    public IReadOnlyList<string> SourcePaths { get; init; } = [];

    /// <summary><c>Novolis.Platform.slnx</c> path when platform-scoped.</summary>
    public string? PlatformSlnxPath { get; init; }

    /// <summary>Flag threshold used.</summary>
    public required double Threshold { get; init; }

    /// <summary>Degree of parallelism used for parse/score.</summary>
    public int DegreeOfParallelism { get; init; }

    /// <summary>All scored methods (sorted by score descending).</summary>
    public required IReadOnlyList<CrapMethodEntry> Methods { get; init; }

    /// <summary>Count of methods with score &gt; threshold.</summary>
    public int FlaggedCount => Methods.Count(m => m.Flagged);

    /// <summary>Highest CRAP among methods (0 when empty).</summary>
    public double MaxScore => Methods.Count == 0 ? 0 : Methods[0].Score;
}
