using System.Text;

namespace Novolis.Tools.Docs.Mermaid;

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
