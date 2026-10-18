using Novolis.Visual.Assets;

namespace Novolis.AssetStudio.Authoring;

/// <summary>Creates or replaces an asset child instance context.</summary>
public sealed record SetAssetInstanceCommand : IAssetStudioCommand
{
    public SetAssetInstanceCommand(AssetInstance? instance)
    {
        Instance = instance;
    }

    public AssetInstance? Instance { get; }

    public string Description => "Set asset instance";

    public AssetStudioDocument Apply(AssetStudioDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        return document.WithAssetInstance(Instance);
    }
}
