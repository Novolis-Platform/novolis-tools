using Novolis.Visual.Materials;

namespace Novolis.AssetStudio.Authoring;

/// <summary>Updates one material layer's projection metadata.</summary>
public sealed record SetMaterialLayerCommand : IAssetStudioCommand
{
    public SetMaterialLayerCommand(MaterialLayer layer)
    {
        Layer = layer ?? throw new ArgumentNullException(nameof(layer));
    }

    public MaterialLayer Layer { get; }

    public string Description => "Set material layer";

    public AssetStudioDocument Apply(AssetStudioDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (document.Material is not { } material)
        {
            throw new InvalidOperationException(
                "Material layers require a material document.");
        }

        return document.WithMaterial(
            AssetStudioMaterialEditor.SetLayer(material, Layer));
    }
}
