namespace Novolis.AssetStudio.Authoring;

/// <summary>Removes one generic component from an asset document.</summary>
public sealed record RemoveAssetComponentCommand : IAssetStudioCommand
{
    public RemoveAssetComponentCommand(string componentId)
    {
        ComponentId = Required(componentId);
    }

    public string ComponentId { get; }

    public string Description => "Remove asset component";

    public AssetStudioDocument Apply(AssetStudioDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (document.Asset is not { } asset)
        {
            throw new InvalidOperationException(
                "Asset components require an asset document.");
        }

        return document.WithAsset(
            AssetStudioAssetRecipeEditor.RemoveComponent(
                asset,
                ComponentId));
    }

    private static string Required(string value) =>
        string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException(
                "A component identifier is required.",
                nameof(value))
            : value.Trim();
}
