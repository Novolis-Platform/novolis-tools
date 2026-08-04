using System.Text;

namespace Novolis.Tools.Docs.Markdown;

/// <summary>Fluent Markdown document that prefers fenced Mermaid for structure.</summary>
public sealed class MarkdownDocument
{
    private readonly string _title;
    private readonly List<string> _blocks = [];

    /// <summary>Creates a document with a logical title (used by doc packs).</summary>
    public MarkdownDocument(string title)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        _title = title.Trim();
    }

    /// <summary>Document title.</summary>
    public string Title => _title;

    /// <summary>Appends an ATX heading.</summary>
    public MarkdownDocument Heading(int level, string text)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(level, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(level, 6);
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        _blocks.Add($"{new string('#', level)} {text.Trim()}");
        _blocks.Add(string.Empty);
        return this;
    }

    /// <inheritdoc cref="Heading"/>
    public MarkdownDocument H1(string text) => Heading(1, text);

    /// <inheritdoc cref="Heading"/>
    public MarkdownDocument H2(string text) => Heading(2, text);

    /// <inheritdoc cref="Heading"/>
    public MarkdownDocument H3(string text) => Heading(3, text);

    /// <summary>Appends a paragraph.</summary>
    public MarkdownDocument P(string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        _blocks.Add(text.Trim());
        _blocks.Add(string.Empty);
        return this;
    }

    /// <summary>Appends a bullet list.</summary>
    public MarkdownDocument Bullets(params string[] items)
    {
        ArgumentNullException.ThrowIfNull(items);
        foreach (var item in items)
        {
            if (string.IsNullOrWhiteSpace(item))
            {
                continue;
            }

            _blocks.Add($"- {item.Trim()}");
        }

        _blocks.Add(string.Empty);
        return this;
    }

    /// <summary>Appends a numbered list.</summary>
    public MarkdownDocument Numbered(params string[] items)
    {
        ArgumentNullException.ThrowIfNull(items);
        var n = 1;
        foreach (var item in items)
        {
            if (string.IsNullOrWhiteSpace(item))
            {
                continue;
            }

            _blocks.Add($"{n}. {item.Trim()}");
            n++;
        }

        _blocks.Add(string.Empty);
        return this;
    }

    /// <summary>Appends a GitHub-flavored Markdown table.</summary>
    public MarkdownDocument Table(IReadOnlyList<string> headers, IEnumerable<IReadOnlyList<string>> rows)
    {
        ArgumentNullException.ThrowIfNull(headers);
        ArgumentNullException.ThrowIfNull(rows);
        if (headers.Count == 0)
        {
            throw new ArgumentException("Table requires at least one header.", nameof(headers));
        }

        _blocks.Add("| " + string.Join(" | ", headers.Select(EscapeCell)) + " |");
        _blocks.Add("| " + string.Join(" | ", headers.Select(_ => "---")) + " |");
        foreach (var row in rows)
        {
            var cells = new string[headers.Count];
            for (var i = 0; i < headers.Count; i++)
            {
                cells[i] = i < row.Count ? EscapeCell(row[i]) : string.Empty;
            }

            _blocks.Add("| " + string.Join(" | ", cells) + " |");
        }

        _blocks.Add(string.Empty);
        return this;
    }

    /// <summary>Appends a fenced code block.</summary>
    public MarkdownDocument Code(string language, string body)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(language);
        ArgumentNullException.ThrowIfNull(body);
        _blocks.Add($"```{language.Trim()}");
        _blocks.Add(body.Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd());
        _blocks.Add("```");
        _blocks.Add(string.Empty);
        return this;
    }

    /// <summary>Appends a fenced Mermaid diagram block.</summary>
    public MarkdownDocument Mermaid(string mermaidSource)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(mermaidSource);
        return Code("mermaid", mermaidSource);
    }

    /// <summary>Appends a horizontal rule.</summary>
    public MarkdownDocument Rule()
    {
        _blocks.Add("---");
        _blocks.Add(string.Empty);
        return this;
    }

    /// <summary>Appends a blockquote.</summary>
    public MarkdownDocument Quote(string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        foreach (var line in text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'))
        {
            _blocks.Add($"> {line}");
        }

        _blocks.Add(string.Empty);
        return this;
    }

    /// <summary>Appends a relative Markdown link paragraph.</summary>
    public MarkdownDocument Link(string label, string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        _blocks.Add($"[{label.Trim()}]({path.Trim()})");
        _blocks.Add(string.Empty);
        return this;
    }

    /// <summary>Appends raw Markdown (caller owns formatting).</summary>
    public MarkdownDocument Raw(string markdown)
    {
        ArgumentNullException.ThrowIfNull(markdown);
        _blocks.Add(markdown.TrimEnd());
        _blocks.Add(string.Empty);
        return this;
    }

    /// <summary>Renders the document as Markdown text.</summary>
    public string ToMarkdown()
    {
        var sb = new StringBuilder();
        foreach (var block in _blocks)
        {
            sb.AppendLine(block);
        }

        return sb.ToString().TrimEnd() + Environment.NewLine;
    }

    /// <inheritdoc />
    public override string ToString() => ToMarkdown();

    private static string EscapeCell(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return value.Replace("|", "\\|", StringComparison.Ordinal).Replace("\n", " ", StringComparison.Ordinal);
    }
}
