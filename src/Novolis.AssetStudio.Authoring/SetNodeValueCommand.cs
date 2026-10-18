using Novolis.Visual.Definitions;

namespace Novolis.AssetStudio.Authoring;

/// <summary>
/// Replaces one node value in a document's semantic recipe.
/// </summary>
public sealed record SetNodeValueCommand : IAssetStudioCommand
{
    /// <summary>
    /// Creates a node-value command.
    /// </summary>
    public SetNodeValueCommand(NodeId nodeId, string name, VisualValue value)
    {
        if (nodeId.IsEmpty)
        {
            throw new ArgumentException(
                "A node identifier is required.",
                nameof(nodeId));
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("A value name is required.", nameof(name));
        }

        ArgumentNullException.ThrowIfNull(value);
        NodeId = nodeId;
        Name = name;
        Value = value;
    }

    /// <summary>
    /// Node to update.
    /// </summary>
    public NodeId NodeId { get; }

    /// <summary>
    /// Value name to update.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Replacement value.
    /// </summary>
    public VisualValue Value { get; }

    /// <inheritdoc />
    public string Description => "Set node value";

    /// <inheritdoc />
    public AssetStudioDocument Apply(AssetStudioDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        return document.WithRecipe(
            AssetStudioRecipeEditor.SetNodeValue(
                document.Recipe,
                NodeId,
                Name,
                Value));
    }
}
