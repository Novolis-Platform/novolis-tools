using System.Diagnostics;
using System.Text.RegularExpressions;
using Novolis.Tools.Docs.Org;
using Novolis.Tools.Docs.Site;

namespace Novolis.Tools.Docs.Marketing;

/// <summary>Options for <see cref="RepoMarketingUpgrader"/>.</summary>
public sealed class RepoMarketingOptions
{
    /// <summary>Workspace root containing novolis-* checkouts.</summary>
    public required string WorkspaceRoot { get; init; }

    /// <summary>Path to Novolis-Platform/.github checkout.</summary>
    public required string GitHubBrandRoot { get; init; }

    /// <summary>Path to governance graphical-profile profile.json.</summary>
    public required string ProfilePath { get; init; }

    /// <summary>Path to sync-repo-package-index-readme.ps1.</summary>
    public required string PackageIndexScriptPath { get; init; }

    /// <summary>When set, updates GitHub repo description and topics via gh.</summary>
    public bool ApplyGitHubMeta { get; init; }

    /// <summary>Skip SVG banner generation.</summary>
    public bool SkipBanners { get; init; }

    /// <summary>Write banners and catalog only.</summary>
    public bool SkipReadmes { get; init; }
}
