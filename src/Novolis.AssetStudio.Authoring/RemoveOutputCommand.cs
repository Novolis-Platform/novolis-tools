namespace Novolis.AssetStudio.Authoring;

/// <summary>
/// Removes one named output from a document's semantic recipe.
/// </summary>
public sealed record RemoveOutputCommand : IAssetStudioCommand
{
    /// <summary>
    /// Creates a remove-output command.
    /// </summary>
    public RemoveOutputCommand(string outputId)
    {
        if (string.IsNullOrWhiteSpace(outputId))
        {
            throw new ArgumentException(
                "An output identifier is required.",
                nameof(outputId));
        }

        OutputId = outputId;
    }

    /// <summary>
    /// Output to remove.
    /// </summary>
    public string OutputId { get; }

    /// <inheritdoc />
    public string Description => "Remove output";

    /// <inheritdoc />
    public AssetStudioDocument Apply(AssetStudioDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        return document.WithRecipe(
            AssetStudioRecipeEditor.RemoveOutput(
                document.Recipe,
                OutputId));
    }
}
