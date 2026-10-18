using Novolis.Visual.Definitions;

namespace Novolis.AssetStudio.Authoring;

/// <summary>Sets one instance-editable material override.</summary>
public sealed record SetMaterialInstanceOverrideCommand : IAssetStudioCommand
{
    public SetMaterialInstanceOverrideCommand(
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

    public string Description => "Set material instance override";

    public AssetStudioDocument Apply(AssetStudioDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (document.Material is not { } material)
        {
            throw new InvalidOperationException(
                "Material overrides require a material document.");
        }

        var instance = document.MaterialInstance ??
            new Novolis.Visual.Materials.MaterialInstance(
                material.MaterialId + ".instance",
                material);
        return document.WithMaterialInstance(
            instance.WithOverride(ParameterId, Value));
    }
}
