using System.Text;

namespace Novolis.Tools.Docs.Mermaid;

/// <summary>Fluent Mermaid erDiagram builder.</summary>
public sealed class MermaidErDiagramBuilder
{
    private readonly List<string> _lines = [];

    /// <summary>Declares an entity with attributes (<c>type name</c> lines).</summary>
    public MermaidErDiagramBuilder Entity(string name, params string[] attributes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var id = MermaidIds.SafeId(name).ToUpperInvariant();
        if (attributes is null || attributes.Length == 0)
        {
            _lines.Add($"  {id} {{");
            _lines.Add("  }");
            return this;
        }

        _lines.Add($"  {id} {{");
        foreach (var attr in attributes)
        {
            if (!string.IsNullOrWhiteSpace(attr))
            {
                _lines.Add($"    {attr.Trim()}");
            }
        }

        _lines.Add("  }");
        return this;
    }

    /// <summary>Adds a relationship (<c>||--o{</c> style by default).</summary>
    public MermaidErDiagramBuilder Relates(string from, string to, string label, string cardinality = "||--o{")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(from);
        ArgumentException.ThrowIfNullOrWhiteSpace(to);
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        _lines.Add($"  {MermaidIds.SafeId(from).ToUpperInvariant()} {cardinality} {MermaidIds.SafeId(to).ToUpperInvariant()} : \"{label.Trim()}\"");
        return this;
    }

    /// <summary>Renders Mermaid source.</summary>
    public string Build()
    {
        var sb = new StringBuilder();
        sb.AppendLine("erDiagram");
        foreach (var line in _lines)
        {
            sb.AppendLine(line);
        }

        return sb.ToString().TrimEnd();
    }
}
