namespace Novolis.Tools.Docs.Site;

/// <summary>Options for building a static multi-repo documentation site.</summary>
public sealed class DocsSiteOptions
{
    /// <summary>
    /// Corpus root. Expected layout: <c>{corpus}/{repo}/docs/**/*.md</c>
    /// (as produced by sparse-checkout of each repository's <c>docs/</c> folder).
    /// </summary>
    public required string CorpusDirectory { get; init; }

    /// <summary>Output directory for <c>index.html</c>, <c>docs/*.html</c>, and copied assets.</summary>
    public required string OutputDirectory { get; init; }

    /// <summary>GitHub organization (used for source links and branding text).</summary>
    public string Org { get; init; } = "Novolis-Platform";

    /// <summary>Optional directory containing <c>site.css</c> / <c>site.js</c> (copied to <c>assets/</c>).</summary>
    public string? AssetsDirectory { get; init; }

    /// <summary>Optional brand directory with favicon / logo / banners (copied under <c>assets/brand</c> and <c>assets/banners</c>).</summary>
    public string? BrandDirectory { get; init; }

    /// <summary>
    /// Optional <c>repo-catalog.json</c> (tag / blurb / topics) from Upgrade-RepoMarketingReadmes.ps1.
    /// Used for catalog card copy, banner fallbacks, and docs page topic chips.
    /// </summary>
    public string? CatalogPath { get; init; }

    /// <summary>Default branch name used in GitHub blob URLs.</summary>
    public string DefaultBranch { get; init; } = "main";

    /// <summary>Public site base URL (footer / canonical hints).</summary>
    public string? BaseUrl { get; init; }
}
