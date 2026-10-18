using Novolis.Visual.Definitions;

namespace Novolis.AssetStudio.Authoring;

/// <summary>
/// Removes one node and its dependent semantic bindings.
/// </summary>
public sealed record RemoveNodeCommand : IAssetStudioCommand
{
    /// <summary>
    /// Creates a remove-node command.
    /// </summary>
    public RemoveNodeCommand(NodeId nodeId)
    {
        if (nodeId.IsEmpty)
        {
            throw new ArgumentException(
                "A node identifier is required.",
                nameof(nodeId));
        }

        NodeId = nodeId;
    }

    /// <summary>
    /// Node to remove.
    /// </summary>
    public NodeId NodeId { get; }

    /// <inheritdoc />
    public string Description => "Remove node";

    /// <inheritdoc />
    public AssetStudioDocument Apply(AssetStudioDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        return document.WithRecipe(
            AssetStudioRecipeEditor.RemoveNode(document.Recipe, NodeId));
    }
}
