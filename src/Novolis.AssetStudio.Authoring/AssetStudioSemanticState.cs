using Novolis.Visual.Definitions;

namespace Novolis.AssetStudio.Authoring;

/// <summary>
/// Immutable semantic state for one Asset Studio document.
/// </summary>
public sealed record AssetStudioSemanticState
{
    /// <summary>
    /// Creates semantic state at a specified revision.
    /// </summary>
    public AssetStudioSemanticState(VisualRecipe recipe, long revision = 0)
    {
        ArgumentNullException.ThrowIfNull(recipe);
        if (revision < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(revision));
        }

        Recipe = recipe;
        Revision = revision;
    }

    /// <summary>
    /// The canonical visual recipe.
    /// </summary>
    public VisualRecipe Recipe { get; }

    /// <summary>
    /// Monotonically increasing semantic revision within the document.
    /// </summary>
    public long Revision { get; }

    /// <summary>
    /// The canonical semantic fingerprint.
    /// </summary>
    public string Fingerprint => Recipe.SemanticFingerprint();
}
