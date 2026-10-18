namespace Novolis.AssetStudio.Authoring;

/// <summary>
/// A deterministic semantic or view edit that can be placed in session
/// history.
/// </summary>
public interface IAssetStudioCommand
{
    /// <summary>
    /// Short stable description of the edit.
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Applies the edit to a document.
    /// </summary>
    AssetStudioDocument Apply(AssetStudioDocument document);
}
