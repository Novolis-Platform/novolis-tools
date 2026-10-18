namespace Novolis.AssetStudio.Authoring;

/// <summary>
/// A reversible edit to the exposed controls of an assembled asset instance.
/// </summary>
public interface IAssetStudioAssetCommand
{
    /// <summary>Short user-facing description of the edit.</summary>
    string Description { get; }

    /// <summary>Applies the edit to an immutable asset session.</summary>
    AssetStudioAssetSession Apply(AssetStudioAssetSession session);
}
