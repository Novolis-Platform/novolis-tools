using System.Text;

namespace Novolis.Tools.Docs.Mermaid;

/// <summary>Fluent Mermaid mindmap builder.</summary>
public sealed class MermaidMindmapBuilder
{
    private string _root = "root";
    private readonly List<(int Depth, string Text)> _nodes = [];

    /// <summary>Sets the root label.</summary>
    public MermaidMindmapBuilder Root(string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        _root = text.Trim();
        return this;
    }

    /// <summary>Adds a node at the given depth (1 = under root).</summary>
    public MermaidMindmapBuilder Node(int depth, string text)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(depth, 1);
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        _nodes.Add((depth, text.Trim()));
        return this;
    }

    /// <summary>Renders Mermaid source.</summary>
    public string Build()
    {
        var sb = new StringBuilder();
        sb.AppendLine("mindmap");
        sb.AppendLine($"  root(({Escape(_root)}))");
        foreach (var (depth, text) in _nodes)
        {
            sb.AppendLine($"{new string(' ', (depth + 1) * 2)}{Escape(text)}");
        }

        return sb.ToString().TrimEnd();
    }

    private static string Escape(string text)
        => text.Replace("(", "[", StringComparison.Ordinal).Replace(")", "]", StringComparison.Ordinal);
}
