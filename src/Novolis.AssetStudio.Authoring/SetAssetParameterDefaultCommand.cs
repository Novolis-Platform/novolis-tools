using Novolis.Visual.Definitions;

namespace Novolis.AssetStudio.Authoring;

/// <summary>Changes an authored asset control and its graph default.</summary>
public sealed record SetAssetParameterDefaultCommand
    : IAssetStudioCommand
{
    public SetAssetParameterDefaultCommand(
        string parameterId,
        VisualValue value)
    {
        ParameterId = string.IsNullOrWhiteSpace(parameterId)
            ? throw new ArgumentException(
                "A parameter identifier is required.",
                nameof(parameterId))
            : parameterId.Trim();
        Value = value ?? throw new ArgumentNullException(nameof(value));
    }

    public string ParameterId { get; }

    public VisualValue Value { get; }

    public string Description => "Set asset control";

    public AssetStudioDocument Apply(AssetStudioDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (document.Asset is not { } asset)
        {
            throw new InvalidOperationException(
                "Asset controls require an asset document.");
        }

        return document.WithAsset(
            AssetStudioAssetRecipeEditor.SetParameterDefault(
                asset,
                ParameterId,
                Value));
    }
}
