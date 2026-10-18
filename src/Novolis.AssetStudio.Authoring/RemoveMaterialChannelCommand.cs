using Novolis.Visual.Materials;

namespace Novolis.AssetStudio.Authoring;

/// <summary>Removes a material channel projection.</summary>
public sealed record RemoveMaterialChannelCommand : IAssetStudioCommand
{
    public RemoveMaterialChannelCommand(MaterialChannel channel)
    {
        Channel = channel;
    }

    public MaterialChannel Channel { get; }

    public string Description => "Remove material channel";

    public AssetStudioDocument Apply(AssetStudioDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (document.Material is not { } material)
        {
            throw new InvalidOperationException(
                "Material channels require a material document.");
        }

        return document.WithMaterial(
            AssetStudioMaterialEditor.RemoveChannel(material, Channel));
    }
}
