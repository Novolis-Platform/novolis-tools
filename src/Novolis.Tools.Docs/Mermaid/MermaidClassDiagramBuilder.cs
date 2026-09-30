using System.Text;

namespace Novolis.Tools.Docs.Mermaid;

/// <summary>Fluent Mermaid classDiagram builder.</summary>
public sealed class MermaidClassDiagramBuilder
{
    private readonly List<string> _lines = [];

    /// <summary>Declares a class with optional members.</summary>
    public MermaidClassDiagramBuilder Class(string name, params string[] members)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var id = MermaidIds.SafeId(name);
        if (members is null || members.Length == 0)
        {
            _lines.Add($"  class {id}");
            return this;
        }

        _lines.Add($"  class {id} {{");
        foreach (var member in members)
        {
            if (!string.IsNullOrWhiteSpace(member))
            {
                _lines.Add($"    {member.Trim()}");
            }
        }

        _lines.Add("  }");
        return this;
    }

    /// <summary>Adds an inheritance edge (<c>&lt;|--</c>).</summary>
    public MermaidClassDiagramBuilder Inherits(string child, string parent)
    {
        _lines.Add($"  {MermaidIds.SafeId(parent)} <|-- {MermaidIds.SafeId(child)}");
        return this;
    }

    /// <summary>Adds a composition edge.</summary>
    public MermaidClassDiagramBuilder Composes(string owner, string part)
    {
        _lines.Add($"  {MermaidIds.SafeId(owner)} *-- {MermaidIds.SafeId(part)}");
        return this;
    }

    /// <summary>Adds a dependency edge.</summary>
    public MermaidClassDiagramBuilder Depends(string from, string to)
    {
        _lines.Add($"  {MermaidIds.SafeId(from)} ..> {MermaidIds.SafeId(to)}");
        return this;
    }

    /// <summary>Renders Mermaid source.</summary>
    public string Build()
    {
        var sb = new StringBuilder();
        sb.AppendLine("classDiagram");
        foreach (var line in _lines)
        {
            sb.AppendLine(line);
        }

        return sb.ToString().TrimEnd();
    }
}
