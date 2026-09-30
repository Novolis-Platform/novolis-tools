namespace Novolis.Tools.Docs.Graph;

/// <summary>A node in a <see cref="RelationshipGraph"/>.</summary>
/// <param name="Id">Stable identifier (safe for Mermaid ids after sanitization).</param>
/// <param name="Label">Human-readable label.</param>
/// <param name="Kind">Semantic kind.</param>
public sealed record GraphNode(string Id, string Label, GraphNodeKind Kind = GraphNodeKind.Unknown);
