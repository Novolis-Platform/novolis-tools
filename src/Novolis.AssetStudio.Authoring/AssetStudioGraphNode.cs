using Novolis.Visual.Definitions;

namespace Novolis.AssetStudio.Authoring;

/// <summary>
/// A deterministic graph projection node.
/// </summary>
public sealed record AssetStudioGraphNode
{
    /// <summary>
    /// Creates a graph node projection.
    /// </summary>
    public AssetStudioGraphNode(
        NodeId id,
        string typeId,
        int version,
        VisualPoint? position,
        bool selected)
    {
        if (id.IsEmpty)
        {
            throw new ArgumentException(
                "A graph node identifier is required.",
                nameof(id));
        }

        if (string.IsNullOrWhiteSpace(typeId))
        {
            throw new ArgumentException("A node type is required.", nameof(typeId));
        }

        if (version < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(version));
        }

        Id = id;
        TypeId = typeId;
        Version = version;
        Position = position;
        Selected = selected;
    }

    /// <summary>
    /// Node identifier.
    /// </summary>
    public NodeId Id { get; }

    /// <summary>
    /// Node type identifier.
    /// </summary>
    public string TypeId { get; }

    /// <summary>
    /// Node schema version.
    /// </summary>
    public int Version { get; }

    /// <summary>
    /// Optional editor position.
    /// </summary>
    public VisualPoint? Position { get; }

    /// <summary>
    /// Whether the node is selected in the view projection.
    /// </summary>
    public bool Selected { get; }
}
