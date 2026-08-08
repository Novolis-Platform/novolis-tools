namespace Novolis.Tools.Docs.Site;

/// <summary>One Markdown documentation page discovered in a multi-repo corpus.</summary>
public sealed class DocsSitePage
{
    /// <summary>Owning repository name (folder under the corpus root).</summary>
    public required string Repo { get; init; }

    /// <summary>Path relative to the repo root (e.g. <c>docs/getting-started.md</c>).</summary>
    public required string RelativePath { get; init; }

    /// <summary>Path relative to <c>docs/</c> (e.g. <c>getting-started.md</c> or <c>README.md</c>).</summary>
    public required string DocsRelativePath { get; init; }

    /// <summary>Site-relative output path (e.g. <c>novolis-raylib/getting-started.html</c> or <c>novolis-raylib/index.html</c>).</summary>
    public required string OutputRelativePath { get; init; }

    /// <summary>Display title (first H1 when present).</summary>
    public required string Title { get; init; }

    /// <summary>Raw Markdown source.</summary>
    public required string Markdown { get; init; }

    /// <summary>GitHub blob URL for the source file.</summary>
    public required string SourceUrl { get; init; }

    /// <summary>True when this page is the docs landing page (<c>docs/README.md</c> or generated overview).</summary>
    public bool IsLanding { get; init; }

    /// <summary>True when the landing page was synthesized because <c>docs/README.md</c> was missing.</summary>
    public bool IsGeneratedLanding { get; init; }
}
