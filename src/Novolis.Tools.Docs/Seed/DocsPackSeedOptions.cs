using Novolis.Tools.Docs.Marketing;
using Novolis.Tools.Docs.Org;
using Novolis.Tools.Docs.Site;

namespace Novolis.Tools.Docs.Seed;

/// <summary>Options for seeding policy docs packs across the workspace.</summary>
public sealed class DocsPackSeedOptions
{
    /// <summary>Workspace root containing novolis-* checkouts.</summary>
    public required string WorkspaceRoot { get; init; }

    /// <summary>Path to Novolis-Platform/.github (for repo-catalog.json).</summary>
    public required string GitHubBrandRoot { get; init; }

    /// <summary>Replace thin/stub docs when present.</summary>
    public bool OverwriteThin { get; init; }

    /// <summary>Skip root README marketing refresh.</summary>
    public bool SkipMarketing { get; init; }

    /// <summary>When non-empty, only process these repository folder names.</summary>
    public IReadOnlyList<string> OnlyRepos { get; init; } = [];
}
