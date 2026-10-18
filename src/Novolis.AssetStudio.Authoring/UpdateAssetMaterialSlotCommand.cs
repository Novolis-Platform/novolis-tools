using Novolis.Visual.Assets;

namespace Novolis.AssetStudio.Authoring;

/// <summary>Updates the generic fields of one asset material slot.</summary>
public sealed record UpdateAssetMaterialSlotCommand : IAssetStudioCommand
{
    public UpdateAssetMaterialSlotCommand(AssetMaterialSlot slot)
    {
        Slot = slot ?? throw new ArgumentNullException(nameof(slot));
    }

    public AssetMaterialSlot Slot { get; }

    public string Description => "Update material slot";

    public AssetStudioDocument Apply(AssetStudioDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (document.Asset is not { } asset)
        {
            throw new InvalidOperationException(
                "Material slots require an asset document.");
        }

        return document.WithAsset(
            AssetStudioAssetRecipeEditor.UpdateSlot(asset, Slot));
    }
}
