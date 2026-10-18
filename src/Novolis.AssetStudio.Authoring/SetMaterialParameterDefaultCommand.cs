using Novolis.Visual.Definitions;

namespace Novolis.AssetStudio.Authoring;

/// <summary>Sets one exposed material control and its graph default.</summary>
public sealed record SetMaterialParameterDefaultCommand
    : IAssetStudioCommand
{
    public SetMaterialParameterDefaultCommand(
        string parameterId,
        VisualValue value)
    {
        ParameterId = Required(parameterId, nameof(parameterId));
        Value = value ?? throw new ArgumentNullException(nameof(value));
    }

    public string ParameterId { get; }

    public VisualValue Value { get; }

    public string Description => "Set material control";

    public AssetStudioDocument Apply(AssetStudioDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (document.Material is not { } material)
        {
            throw new InvalidOperationException(
                "Material controls require a material document.");
        }

        return document.WithMaterial(
            AssetStudioMaterialEditor.SetParameterDefault(
                material,
                ParameterId,
                Value));
    }

    private static string Required(string value, string name) =>
        string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException(
                "A parameter identifier is required.",
                name)
            : value.Trim();
}
