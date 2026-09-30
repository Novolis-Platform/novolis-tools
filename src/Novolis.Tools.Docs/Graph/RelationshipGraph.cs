namespace Novolis.Tools.Docs.Graph;

/// <summary>Mutable relationship graph that renders to Markdown + Mermaid.</summary>
public sealed class RelationshipGraph
{
    private readonly Dictionary<string, GraphNode> _nodes = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<GraphEdge> _edges = [];

    /// <summary>Nodes in insertion order.</summary>
    public IReadOnlyCollection<GraphNode> Nodes => _nodes.Values;

    /// <summary>Edges in insertion order.</summary>
    public IReadOnlyList<GraphEdge> Edges => _edges;

    /// <summary>Adds or replaces a node.</summary>
    public RelationshipGraph AddNode(GraphNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentException.ThrowIfNullOrWhiteSpace(node.Id);
        _nodes[node.Id] = node;
        return this;
    }

    /// <summary>Adds a node by id/label/kind.</summary>
    public RelationshipGraph AddNode(string id, string label, GraphNodeKind kind = GraphNodeKind.Unknown)
        => AddNode(new GraphNode(id, label, kind));

    /// <summary>Adds a directed edge; creates missing nodes with the given ids as labels.</summary>
    public RelationshipGraph AddEdge(string fromId, string toId, string? label = null, GraphNodeKind? fromKind = null, GraphNodeKind? toKind = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fromId);
        ArgumentException.ThrowIfNullOrWhiteSpace(toId);
        if (!_nodes.ContainsKey(fromId))
        {
            AddNode(fromId, fromId, fromKind ?? GraphNodeKind.Unknown);
        }

        if (!_nodes.ContainsKey(toId))
        {
            AddNode(toId, toId, toKind ?? GraphNodeKind.Unknown);
        }

        _edges.Add(new GraphEdge(fromId, toId, label));
        return this;
    }

    /// <summary>Renders a Mermaid flowchart for the whole graph.</summary>
    public string ToMermaidFlowchart(FlowDirectionHint direction = FlowDirectionHint.LR)
    {
        var dir = direction switch
        {
            FlowDirectionHint.TB => Mermaid.FlowDirection.TB,
            FlowDirectionHint.BT => Mermaid.FlowDirection.BT,
            FlowDirectionHint.RL => Mermaid.FlowDirection.RL,
            _ => Mermaid.FlowDirection.LR,
        };

        return Mermaid.MermaidDiagram.Flowchart(chart =>
        {
            chart.Direction(dir);
            foreach (var node in _nodes.Values)
            {
                var shape = node.Kind switch
                {
                    GraphNodeKind.Package => "stadium",
                    GraphNodeKind.Area => "round",
                    GraphNodeKind.External => "hex",
                    _ => "rect",
                };
                chart.Node(node.Id, node.Label, shape);
            }

            foreach (var edge in _edges)
            {
                if (string.Equals(edge.Label, "PackageReference", StringComparison.OrdinalIgnoreCase))
                {
                    chart.Depends(edge.FromId, edge.ToId, edge.Label);
                }
                else
                {
                    chart.Edge(edge.FromId, edge.ToId, edge.Label);
                }
            }
        });
    }

    /// <summary>Renders an edge table as Markdown rows (header separate).</summary>
    public IEnumerable<IReadOnlyList<string>> ToEdgeTableRows()
    {
        foreach (var edge in _edges)
        {
            var from = _nodes.TryGetValue(edge.FromId, out var f) ? f.Label : edge.FromId;
            var to = _nodes.TryGetValue(edge.ToId, out var t) ? t.Label : edge.ToId;
            yield return [from, to, edge.Label ?? string.Empty];
        }
    }
}
