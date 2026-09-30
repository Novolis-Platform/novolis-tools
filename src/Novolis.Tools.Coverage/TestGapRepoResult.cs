using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Novolis.Tools.Coverage;

/// <summary>One repo in a test-gap report.</summary>
public sealed class TestGapRepoResult
{
    /// <summary>Folder name.</summary>
    public required string Repo { get; init; }

    /// <summary>Absolute path.</summary>
    public required string Path { get; init; }

    /// <summary>Root-level slnx file names.</summary>
    public IReadOnlyList<string> Solutions { get; init; } = [];

    /// <summary>Test host count.</summary>
    public int TestHostCount { get; init; }

    /// <summary>Production assembly count.</summary>
    public int ProductionCount { get; init; }

    /// <summary>Assemblies referenced by a test host.</summary>
    public int TestedCount { get; init; }

    /// <summary>Assemblies not referenced by a test host.</summary>
    public int UntestedCount { get; init; }

    /// <summary>True when the repo has product code but no test host.</summary>
    public bool NoTestHosts { get; init; }

    /// <summary>Untested production assemblies.</summary>
    public IReadOnlyList<TestGapAssembly> Untested { get; init; } = [];

    /// <summary>Linked percent, or null when no production assemblies.</summary>
    public double? CoveragePct { get; init; }
}
