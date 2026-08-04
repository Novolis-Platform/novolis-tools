using System.Text;

namespace Novolis.Tools.Docs.Mermaid;

/// <summary>Flowchart / graph direction for Mermaid.</summary>
public enum FlowDirection
{
    /// <summary>Top to bottom.</summary>
    TB,

    /// <summary>Bottom to top.</summary>
    BT,

    /// <summary>Left to right.</summary>
    LR,

    /// <summary>Right to left.</summary>
    RL,
}

/// <summary>Factory for Mermaid diagram source strings.</summary>
public static class MermaidDiagram
{
    /// <summary>Builds a flowchart via a fluent builder.</summary>
    public static string Flowchart(Action<MermaidFlowchartBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        var builder = new MermaidFlowchartBuilder();
        configure(builder);
        return builder.Build();
    }

    /// <summary>Builds a class diagram via a fluent builder.</summary>
    public static string ClassDiagram(Action<MermaidClassDiagramBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        var builder = new MermaidClassDiagramBuilder();
        configure(builder);
        return builder.Build();
    }

    /// <summary>Builds an ER diagram via a fluent builder.</summary>
    public static string ErDiagram(Action<MermaidErDiagramBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        var builder = new MermaidErDiagramBuilder();
        configure(builder);
        return builder.Build();
    }

    /// <summary>Builds a mindmap via a fluent builder.</summary>
    public static string Mindmap(Action<MermaidMindmapBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        var builder = new MermaidMindmapBuilder();
        configure(builder);
        return builder.Build();
    }
}

/// <summary>Fluent Mermaid flowchart builder.</summary>
public sealed class MermaidFlowchartBuilder
{
    private FlowDirection _direction = FlowDirection.TB;
    private readonly List<string> _lines = [];
    private readonly HashSet<string> _declared = new(StringComparer.Ordinal);

    /// <summary>Sets flowchart direction.</summary>
    public MermaidFlowchartBuilder Direction(FlowDirection direction)
    {
        _direction = direction;
        return this;
    }

    /// <summary>Declares a node with an optional display label.</summary>
    public MermaidFlowchartBuilder Node(string id, string? label = null, string shape = "rect")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        var safeId = MermaidIds.SafeId(id);
        var text = label ?? id;
        var line = shape switch
        {
            "round" => $"  {safeId}(\"{Escape(text)}\")",
            "stadium" => $"  {safeId}([\"{Escape(text)}\"])",
            "circle" => $"  {safeId}((\"{Escape(text)}\"))",
            "rhombus" => $"  {safeId}{{\"{Escape(text)}\"}}",
            "hex" => $"  {safeId}{{{{{Escape(text)}}}}}",
            _ => $"  {safeId}[\"{Escape(text)}\"]",
        };
        _lines.Add(line);
        _declared.Add(safeId);
        return this;
    }

    /// <summary>Adds a directed edge.</summary>
    public MermaidFlowchartBuilder Edge(string from, string to, string? label = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(from);
        ArgumentException.ThrowIfNullOrWhiteSpace(to);
        var a = MermaidIds.SafeId(from);
        var b = MermaidIds.SafeId(to);
        EnsureImplicit(a, from);
        EnsureImplicit(b, to);
        _lines.Add(string.IsNullOrWhiteSpace(label)
            ? $"  {a} --> {b}"
            : $"  {a} -->|\"{Escape(label)}\"| {b}");
        return this;
    }

    /// <summary>Adds a dashed dependency edge.</summary>
    public MermaidFlowchartBuilder Depends(string from, string to, string? label = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(from);
        ArgumentException.ThrowIfNullOrWhiteSpace(to);
        var a = MermaidIds.SafeId(from);
        var b = MermaidIds.SafeId(to);
        EnsureImplicit(a, from);
        EnsureImplicit(b, to);
        _lines.Add(string.IsNullOrWhiteSpace(label)
            ? $"  {a} -.-> {b}"
            : $"  {a} -.->|\"{Escape(label)}\"| {b}");
        return this;
    }

    /// <summary>Renders Mermaid source.</summary>
    public string Build()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"flowchart {_direction}");
        foreach (var line in _lines)
        {
            sb.AppendLine(line);
        }

        return sb.ToString().TrimEnd();
    }

    private void EnsureImplicit(string safeId, string label)
    {
        if (_declared.Add(safeId))
        {
            _lines.Add($"  {safeId}[\"{Escape(label)}\"]");
        }
    }

    private static string Escape(string text)
        => text.Replace("\"", "#quot;", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal);
}

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

internal static class MermaidIds
{
    public static string SafeId(string value)
    {
        var sb = new StringBuilder(value.Length);
        foreach (var ch in value.Trim())
        {
            if (char.IsLetterOrDigit(ch) || ch is '_' or '-')
            {
                sb.Append(ch);
            }
            else
            {
                sb.Append('_');
            }
        }

        if (sb.Length == 0)
        {
            return "n0";
        }

        if (char.IsDigit(sb[0]))
        {
            sb.Insert(0, 'n');
        }

        return sb.ToString();
    }
}
