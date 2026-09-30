using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Novolis.Tools.Coverage;

/// <summary>Workspace test-gap scan result.</summary>
public sealed class TestGapReport
{
    /// <summary>Workspace root.</summary>
    public required string Root { get; init; }

    /// <summary>UTC generation time.</summary>
    public DateTime GeneratedUtc { get; init; }

    /// <summary>Whether non-packable libraries were ignored.</summary>
    public bool PackableOnly { get; init; }

    /// <summary>Per-repo results.</summary>
    public IReadOnlyList<TestGapRepoResult> Repos { get; init; } = [];

    /// <summary>Repos that have src/codegen or slnx but no test host.</summary>
    public IReadOnlyList<TestGapRepoResult> ReposWithoutTestHosts =>
        Repos.Where(r => r.NoTestHosts).ToArray();

    /// <summary>Production assemblies with no direct test ProjectReference.</summary>
    public IReadOnlyList<TestGapAssembly> UntestedAssemblies =>
        Repos.SelectMany(r => r.Untested).ToArray();

    /// <summary>Total repos without tests plus untested assemblies.</summary>
    public int GapCount => ReposWithoutTestHosts.Count + UntestedAssemblies.Count;
}
