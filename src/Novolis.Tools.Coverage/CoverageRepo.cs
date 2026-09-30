namespace Novolis.Tools.Coverage;

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
