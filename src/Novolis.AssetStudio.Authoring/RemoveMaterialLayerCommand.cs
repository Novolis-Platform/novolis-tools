namespace Novolis.AssetStudio.Authoring;

/// <summary>Removes one material layer projection.</summary>
public sealed record RemoveMaterialLayerCommand : IAssetStudioCommand
{
    public RemoveMaterialLayerCommand(string layerId)
    {
        LayerId = string.IsNullOrWhiteSpace(layerId)
            ? throw new ArgumentException(
                "A layer identifier is required.",
                nameof(layerId))
            : layerId.Trim();
    }

    public string LayerId { get; }

    public string Description => "Remove material layer";

    public AssetStudioDocument Apply(AssetStudioDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (document.Material is not { } material)
        {
            throw new InvalidOperationException(
                "Material layers require a material document.");
        }

        return document.WithMaterial(
            AssetStudioMaterialEditor.RemoveLayer(material, LayerId));
    }
}
