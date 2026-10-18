using Novolis.Visual.Definitions;

namespace Novolis.AssetStudio.Authoring;

/// <summary>
/// Replaces one named output in a document's semantic recipe.
/// </summary>
public sealed record UpdateOutputCommand : IAssetStudioCommand
{
    /// <summary>
    /// Creates an output update command.
    /// </summary>
    public UpdateOutputCommand(VisualOutput output)
    {
        ArgumentNullException.ThrowIfNull(output);
        Output = output;
    }

    /// <summary>
    /// Replacement output.
    /// </summary>
    public VisualOutput Output { get; }

    /// <inheritdoc />
    public string Description => "Update output";

    /// <inheritdoc />
    public AssetStudioDocument Apply(AssetStudioDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        return document.WithRecipe(
            AssetStudioRecipeEditor.SetOutput(document.Recipe, Output));
    }
}
