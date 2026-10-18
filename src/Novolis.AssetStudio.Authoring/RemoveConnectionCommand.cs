using Novolis.Visual.Definitions;

namespace Novolis.AssetStudio.Authoring;

/// <summary>
/// Removes one connection from a document's semantic recipe.
/// </summary>
public sealed record RemoveConnectionCommand : IAssetStudioCommand
{
    /// <summary>
    /// Creates a remove-connection command.
    /// </summary>
    public RemoveConnectionCommand(ConnectionId connectionId)
    {
        if (connectionId.IsEmpty)
        {
            throw new ArgumentException(
                "A connection identifier is required.",
                nameof(connectionId));
        }

        ConnectionId = connectionId;
    }

    /// <summary>
    /// Connection to remove.
    /// </summary>
    public ConnectionId ConnectionId { get; }

    /// <inheritdoc />
    public string Description => "Remove connection";

    /// <inheritdoc />
    public AssetStudioDocument Apply(AssetStudioDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        return document.WithRecipe(
            AssetStudioRecipeEditor.RemoveConnection(
                document.Recipe,
                ConnectionId));
    }
}
