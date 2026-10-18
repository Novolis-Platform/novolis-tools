using Novolis.Visual.Definitions;

namespace Novolis.AssetStudio.Authoring;

/// <summary>
/// Replaces the endpoints of one connection as one undoable edit.
/// </summary>
public sealed record ReconnectConnectionCommand : IAssetStudioCommand
{
    public ReconnectConnectionCommand(
        ConnectionId connectionId,
        VisualConnection replacement)
    {
        if (connectionId.IsEmpty)
        {
            throw new ArgumentException(
                "A connection identifier is required.",
                nameof(connectionId));
        }

        ArgumentNullException.ThrowIfNull(replacement);
        ConnectionId = connectionId;
        Replacement = replacement;
    }

    public ConnectionId ConnectionId { get; }

    public VisualConnection Replacement { get; }

    public string Description => "Reconnect ports";

    public AssetStudioDocument Apply(AssetStudioDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var recipe = document.Recipe;
        if (recipe.Connections.Any(
                connection => connection.Id == ConnectionId))
        {
            recipe = AssetStudioRecipeEditor.RemoveConnection(
                recipe,
                ConnectionId);
        }

        if (recipe.Connections.Any(
                connection => connection.Id == Replacement.Id))
        {
            throw new InvalidOperationException(
                $"Connection '{Replacement.Id}' already exists.");
        }

        return document.WithRecipe(
            AssetStudioRecipeEditor.AddConnection(
                recipe,
                Replacement));
    }
}
