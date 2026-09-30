using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Novolis.Tools.Coverage;

/// <summary>One production assembly in a test-gap report.</summary>
public sealed class TestGapAssembly
{
    /// <summary>Owning repo folder name.</summary>
    public required string Repo { get; init; }

    /// <summary>PackageId or project name.</summary>
    public required string PackageId { get; init; }

    /// <summary>Path relative to the repo root.</summary>
    public required string RelPath { get; init; }

    /// <summary>Whether the project is packable.</summary>
    public bool Packable { get; init; }

    /// <summary>Whether a test host ProjectReferences it.</summary>
    public bool Tested { get; init; }
}
