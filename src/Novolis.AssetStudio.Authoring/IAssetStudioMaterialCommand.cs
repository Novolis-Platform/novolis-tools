namespace Novolis.AssetStudio.Authoring;

/// <summary>
/// Immutable edit for a material instance session.
/// </summary>
public interface IAssetStudioMaterialCommand
{
    string Description { get; }

    AssetStudioMaterialSession Apply(AssetStudioMaterialSession session);
}
