using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Novolis.Tools.Coverage;

/// <summary>Options for <see cref="TestGapScanner"/>.</summary>
public sealed class TestGapOptions
{
    /// <summary>Workspace root.</summary>
    public string? Root { get; init; }

    /// <summary>Extra repo names to skip.</summary>
    public string[] Exclude { get; init; } = [];

    /// <summary>Exclude file (default governance coverage-excludes.txt).</summary>
    public string? ExcludeFile { get; init; }

    /// <summary>Only these repos.</summary>
    public string[] Include { get; init; } = [];

    /// <summary>Only flag packable assemblies (default true).</summary>
    public bool PackableOnly { get; init; } = true;

    /// <summary>Include WinExe/Exe projects under src/.</summary>
    public bool IncludeExecutables { get; init; }

    /// <summary>Parallelism for per-repo scans.</summary>
    public int ThrottleLimit { get; init; }
}
