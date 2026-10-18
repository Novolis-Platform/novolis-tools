using Novolis.Visual.Definitions;

namespace Novolis.AssetStudio.Authoring;

/// <summary>
/// Adds one typed connection to a document's semantic recipe.
/// </summary>
public sealed record AddConnectionCommand : IAssetStudioCommand
{
    /// <summary>
    /// Creates an add-connection command.
    /// </summary>
    public AddConnectionCommand(VisualConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);
        Connection = connection;
    }

    /// <summary>
    /// Connection to add.
    /// </summary>
    public VisualConnection Connection { get; }

    /// <inheritdoc />
    public string Description => "Add connection";

    /// <inheritdoc />
    public AssetStudioDocument Apply(AssetStudioDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        return document.WithRecipe(
            AssetStudioRecipeEditor.AddConnection(
                document.Recipe,
                Connection));
    }
}
