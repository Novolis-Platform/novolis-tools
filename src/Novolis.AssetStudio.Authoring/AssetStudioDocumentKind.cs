namespace Novolis.AssetStudio.Authoring;

/// <summary>
/// Identifies the semantic authoring context held by an Asset Studio document.
/// </summary>
public enum AssetStudioDocumentKind
{
    /// <summary>A host-neutral visual recipe without module metadata.</summary>
    Recipe,

    /// <summary>A reusable material module.</summary>
    Material,

    /// <summary>A reusable assembled asset module.</summary>
    Asset,
}
