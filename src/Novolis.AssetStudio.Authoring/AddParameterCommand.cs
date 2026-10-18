using Novolis.Visual.Definitions;

namespace Novolis.AssetStudio.Authoring;

/// <summary>
/// Adds one exposed parameter to a document's semantic recipe.
/// </summary>
public sealed record AddParameterCommand : IAssetStudioCommand
{
    /// <summary>
    /// Creates an add-parameter command.
    /// </summary>
    public AddParameterCommand(VisualParameter parameter)
    {
        ArgumentNullException.ThrowIfNull(parameter);
        Parameter = parameter;
    }

    /// <summary>
    /// Parameter to add.
    /// </summary>
    public VisualParameter Parameter { get; }

    /// <inheritdoc />
    public string Description => "Add parameter";

    /// <inheritdoc />
    public AssetStudioDocument Apply(AssetStudioDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        return document.WithRecipe(
            AssetStudioRecipeEditor.AddParameter(
                document.Recipe,
                Parameter));
    }
}
