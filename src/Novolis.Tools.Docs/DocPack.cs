using Novolis.Tools.Docs.Graph;
using Novolis.Tools.Docs.Markdown;
using Novolis.Tools.Docs.Mermaid;

namespace Novolis.Tools.Docs;

/// <summary>A set of Markdown documents written as a documentation pack.</summary>
public sealed class DocPack
{
    private readonly List<MarkdownDocument> _documents = [];

    /// <summary>Logical pack title.</summary>
    public required string Title { get; init; }

    /// <summary>Documents in write order.</summary>
    public IReadOnlyList<MarkdownDocument> Documents => _documents;

    /// <summary>Adds a document.</summary>
    public DocPack Add(MarkdownDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        _documents.Add(document);
        return this;
    }

    /// <summary>Writes all documents under <paramref name="outputDirectory"/> as <c>*.md</c> files.</summary>
    public IReadOnlyList<string> WriteTo(string outputDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);
        Directory.CreateDirectory(outputDirectory);
        var written = new List<string>();
        foreach (var doc in _documents)
        {
            var fileName = Slug(doc.Title) + ".md";
            var path = Path.Combine(outputDirectory, fileName);
            File.WriteAllText(path, doc.ToMarkdown());
            written.Add(path);
        }

        return written;
    }

    private static string Slug(string title)
    {
        var chars = title.Trim().ToLowerInvariant().Select(ch =>
            char.IsLetterOrDigit(ch) ? ch : '-').ToArray();
        var slug = new string(chars);
        while (slug.Contains("--", StringComparison.Ordinal))
        {
            slug = slug.Replace("--", "-", StringComparison.Ordinal);
        }

        return slug.Trim('-');
    }
}
