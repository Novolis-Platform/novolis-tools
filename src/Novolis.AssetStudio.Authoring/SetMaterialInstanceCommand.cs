using Novolis.Visual.Definitions;
using Novolis.Visual.Materials;

namespace Novolis.AssetStudio.Authoring;

/// <summary>Creates or replaces a material child instance context.</summary>
public sealed record SetMaterialInstanceCommand : IAssetStudioCommand
{
    public SetMaterialInstanceCommand(MaterialInstance? instance)
    {
        Instance = instance;
    }

    public MaterialInstance? Instance { get; }

    public string Description => "Set material instance";

    public AssetStudioDocument Apply(AssetStudioDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        return document.WithMaterialInstance(Instance);
    }
}

