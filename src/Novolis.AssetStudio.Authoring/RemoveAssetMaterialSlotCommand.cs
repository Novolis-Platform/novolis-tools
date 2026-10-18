namespace Novolis.AssetStudio.Authoring;

/// <summary>Removes one material slot from an asset document.</summary>
public sealed record RemoveAssetMaterialSlotCommand : IAssetStudioCommand
{
    public RemoveAssetMaterialSlotCommand(string slotId)
    {
        SlotId = string.IsNullOrWhiteSpace(slotId)
            ? throw new ArgumentException(
                "A slot identifier is required.",
                nameof(slotId))
            : slotId.Trim();
    }

    public string SlotId { get; }

    public string Description => "Remove material slot";

    public AssetStudioDocument Apply(AssetStudioDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (document.Asset is not { } asset)
        {
            throw new InvalidOperationException(
                "Material slots require an asset document.");
        }

        return document.WithAsset(
            AssetStudioAssetRecipeEditor.RemoveSlot(asset, SlotId));
    }
}
