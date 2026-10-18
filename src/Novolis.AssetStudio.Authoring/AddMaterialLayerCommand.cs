using Novolis.Visual.Materials;

namespace Novolis.AssetStudio.Authoring;

/// <summary>Adds a layer projection without creating a second graph model.</summary>
public sealed record AddMaterialLayerCommand : IAssetStudioCommand
{
    public AddMaterialLayerCommand(MaterialLayer layer)
    {
        Layer = layer ?? throw new ArgumentNullException(nameof(layer));
    }

    public MaterialLayer Layer { get; }

    public string Description => "Add material layer";

    public AssetStudioDocument Apply(AssetStudioDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (document.Material is not { } material)
        {
            throw new InvalidOperationException(
                "Material layers require a material document.");
        }

        return document.WithMaterial(
            AssetStudioMaterialEditor.AddLayer(material, Layer));
    }
}
