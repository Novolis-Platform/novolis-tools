namespace Novolis.Tools.Docs.Site;

/// <summary>One Markdown documentation page discovered in a multi-repo corpus.</summary>
public sealed class DocsSitePage
{
    /// <summary>Owning repository name (folder under the corpus root).</summary>
    public required string Repo { get; init; }

    /// <summary>Path relative to the repo root, using forward slashes (e.g. <c>docs/getting-started.md</c>).</summary>
    public required string RelativePath { get; init; }

    /// <summary>Display title (first H1 when present).</summary>
    public required string Title { get; init; }

    /// <summary>URL slug for the generated HTML file (no extension).</summary>
    public required string Slug { get; init; }

    /// <summary>Kind label for filtering (typically <c>Docs</c>).</summary>
    public required string Kind { get; init; }

    /// <summary>Raw Markdown source.</summary>
    public required string Markdown { get; init; }

    /// <summary>GitHub blob URL for the source file.</summary>
    public required string SourceUrl { get; init; }
}
