using Novolis.Visual.Assets;

namespace Novolis.AssetStudio.Authoring;

/// <summary>Adds one generic component to an asset document.</summary>
public sealed record AddAssetComponentCommand : IAssetStudioCommand
{
    public AddAssetComponentCommand(AssetComponent component)
    {
        Component = component ?? throw new ArgumentNullException(nameof(component));
    }

    public AssetComponent Component { get; }

    public string Description => "Add asset component";

    public AssetStudioDocument Apply(AssetStudioDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (document.Asset is not { } asset)
        {
            throw new InvalidOperationException(
                "Asset components require an asset document.");
        }

        return document.WithAsset(
            AssetStudioAssetRecipeEditor.AddComponent(asset, Component));
    }
}
