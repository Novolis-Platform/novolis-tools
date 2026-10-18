using Novolis.Visual.Assets;

namespace Novolis.AssetStudio.Authoring;

/// <summary>Adds one material slot to an asset document.</summary>
public sealed record AddAssetMaterialSlotCommand : IAssetStudioCommand
{
    public AddAssetMaterialSlotCommand(AssetMaterialSlot slot)
    {
        Slot = slot ?? throw new ArgumentNullException(nameof(slot));
    }

    public AssetMaterialSlot Slot { get; }

    public string Description => "Add material slot";

    public AssetStudioDocument Apply(AssetStudioDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (document.Asset is not { } asset)
        {
            throw new InvalidOperationException(
                "Material slots require an asset document.");
        }

        return document.WithAsset(
            AssetStudioAssetRecipeEditor.AddSlot(asset, Slot));
    }
}
