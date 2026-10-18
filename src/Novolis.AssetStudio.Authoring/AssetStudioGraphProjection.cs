using System.Collections.Immutable;

namespace Novolis.AssetStudio.Authoring;

/// <summary>
/// Deterministic graph projection joining semantic topology to view positions.
/// </summary>
public sealed record AssetStudioGraphProjection
{
    private AssetStudioGraphProjection(
        string recipeId,
        long documentRevision,
        ImmutableArray<AssetStudioGraphNode> nodes,
        ImmutableArray<AssetStudioGraphConnection> connections)
    {
        RecipeId = recipeId;
        DocumentRevision = documentRevision;
        Nodes = nodes;
        Connections = connections;
    }

    /// <summary>
    /// Builds a projection from an immutable document.
    /// </summary>
    public static AssetStudioGraphProjection From(AssetStudioDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var nodes = document.Recipe.Nodes
            .OrderBy(node => node.Id)
            .Select(node =>
                new AssetStudioGraphNode(
                    node.Id,
                    node.TypeId,
                    node.Version,
                    document.View.NodePositions.TryGetValue(
                        node.Id,
                        out var position)
                        ? position
                        : null,
                    document.View.SelectedNodes.Contains(node.Id)))
            .ToImmutableArray();
        var connections = document.Recipe.Connections
            .OrderBy(connection => connection.Id)
            .Select(connection =>
                new AssetStudioGraphConnection(
                    connection.Id,
                    connection.Source,
                    connection.Target))
            .ToImmutableArray();
        return new AssetStudioGraphProjection(
            document.Recipe.RecipeId,
            document.Revision,
            nodes,
            connections);
    }

    /// <summary>
    /// Recipe identifier represented by the projection.
    /// </summary>
    public string RecipeId { get; }

    /// <summary>
    /// Document revision represented by the projection.
    /// </summary>
    public long DocumentRevision { get; }

    /// <summary>
    /// Ordered node projections.
    /// </summary>
    public ImmutableArray<AssetStudioGraphNode> Nodes { get; }

    /// <summary>
    /// Ordered connection projections.
    /// </summary>
    public ImmutableArray<AssetStudioGraphConnection> Connections { get; }
}
