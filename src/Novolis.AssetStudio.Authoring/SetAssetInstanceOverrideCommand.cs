using Novolis.Visual.Definitions;

namespace Novolis.AssetStudio.Authoring;

/// <summary>Sets one instance-editable asset override.</summary>
public sealed record SetAssetInstanceOverrideCommand : IAssetStudioCommand
{
    public SetAssetInstanceOverrideCommand(
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

    public string Description => "Set asset instance override";

    public AssetStudioDocument Apply(AssetStudioDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (document.Asset is not { } asset)
        {
            throw new InvalidOperationException(
                "Asset overrides require an asset document.");
        }

        var instance = document.AssetInstance ??
            new Novolis.Visual.Assets.AssetInstance(
                asset.AssetId + ".instance",
                asset);
        return document.WithAssetInstance(
            instance.WithOverride(ParameterId, Value));
    }
}
