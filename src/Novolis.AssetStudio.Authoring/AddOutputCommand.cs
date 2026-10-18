using Novolis.Visual.Definitions;

namespace Novolis.AssetStudio.Authoring;

/// <summary>
/// Adds one named output to a document's semantic recipe.
/// </summary>
public sealed record AddOutputCommand : IAssetStudioCommand
{
    /// <summary>
    /// Creates an add-output command.
    /// </summary>
    public AddOutputCommand(VisualOutput output)
    {
        ArgumentNullException.ThrowIfNull(output);
        Output = output;
    }

    /// <summary>
    /// Output to add.
    /// </summary>
    public VisualOutput Output { get; }

    /// <inheritdoc />
    public string Description => "Add output";

    /// <inheritdoc />
    public AssetStudioDocument Apply(AssetStudioDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        return document.WithRecipe(
            AssetStudioRecipeEditor.AddOutput(document.Recipe, Output));
    }
}
