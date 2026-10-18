namespace Novolis.AssetStudio.Authoring;

/// <summary>
/// Removes one exposed parameter from a document's semantic recipe.
/// </summary>
public sealed record RemoveParameterCommand : IAssetStudioCommand
{
    /// <summary>
    /// Creates a remove-parameter command.
    /// </summary>
    public RemoveParameterCommand(string parameterId)
    {
        if (string.IsNullOrWhiteSpace(parameterId))
        {
            throw new ArgumentException(
                "A parameter identifier is required.",
                nameof(parameterId));
        }

        ParameterId = parameterId;
    }

    /// <summary>
    /// Parameter to remove.
    /// </summary>
    public string ParameterId { get; }

    /// <inheritdoc />
    public string Description => "Remove parameter";

    /// <inheritdoc />
    public AssetStudioDocument Apply(AssetStudioDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        return document.WithRecipe(
            AssetStudioRecipeEditor.RemoveParameter(
                document.Recipe,
                ParameterId));
    }
}
