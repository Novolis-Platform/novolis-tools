using Novolis.Visual.Definitions;

namespace Novolis.AssetStudio.Authoring;

/// <summary>
/// Adds one node to a document's semantic recipe.
/// </summary>
public sealed record AddNodeCommand : IAssetStudioCommand
{
    /// <summary>
    /// Creates an add-node command.
    /// </summary>
    public AddNodeCommand(VisualNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        Node = node;
    }

    /// <summary>
    /// Node to add.
    /// </summary>
    public VisualNode Node { get; }

    /// <inheritdoc />
    public string Description => "Add node";

    /// <inheritdoc />
    public AssetStudioDocument Apply(AssetStudioDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        return document.WithRecipe(
            AssetStudioRecipeEditor.AddNode(document.Recipe, Node));
    }
}
