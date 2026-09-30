using System.Text;

namespace Novolis.Tools.Docs.Mermaid;

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
