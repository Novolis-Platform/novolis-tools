using Novolis.Visual.Definitions;

namespace Novolis.AssetStudio.Authoring;

/// <summary>
/// Replaces one exposed parameter value in a document's semantic recipe.
/// </summary>
public sealed record SetParameterValueCommand : IAssetStudioCommand
{
    /// <summary>
    /// Creates a parameter-value command.
    /// </summary>
    public SetParameterValueCommand(string parameterId, VisualValue value)
    {
        if (string.IsNullOrWhiteSpace(parameterId))
        {
            throw new ArgumentException(
                "A parameter identifier is required.",
                nameof(parameterId));
        }

        ArgumentNullException.ThrowIfNull(value);
        ParameterId = parameterId;
        Value = value;
    }

    /// <summary>
    /// Parameter to update.
    /// </summary>
    public string ParameterId { get; }

    /// <summary>
    /// Replacement value.
    /// </summary>
    public VisualValue Value { get; }

    /// <inheritdoc />
    public string Description => "Set parameter value";

    /// <inheritdoc />
    public AssetStudioDocument Apply(AssetStudioDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        return document.WithRecipe(
            AssetStudioRecipeEditor.SetParameterValue(
                document.Recipe,
                ParameterId,
                Value));
    }
}
