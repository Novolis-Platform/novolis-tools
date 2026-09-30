using System.Text.Json;

namespace Novolis.Tools.Docs.Site;

/// <summary>Marketing metadata for a repository (tagline, blurb, GitHub topics).</summary>
public sealed class DocsRepoMeta
{
    /// <summary>Short tagline shown on banners and cards.</summary>
    public string Tag { get; init; } = string.Empty;

    /// <summary>One-line description for catalog cards.</summary>
    public string Blurb { get; init; } = string.Empty;

    /// <summary>GitHub repository description.</summary>
    public string Desc { get; init; } = string.Empty;

    /// <summary>GitHub topics / docs tags.</summary>
    public IReadOnlyList<string> Topics { get; init; } = [];
}
