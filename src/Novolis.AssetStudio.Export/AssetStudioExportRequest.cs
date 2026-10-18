using Novolis.AssetStudio.Authoring;
using Novolis.Visual.Assets;
using Novolis.Visual.Materials;

namespace Novolis.AssetStudio.Export;

/// <summary>
/// In-memory request for one deterministic export profile.
/// </summary>
public sealed record AssetStudioExportRequest
{
    /// <summary>
    /// Creates an export request.
    /// </summary>
    public AssetStudioExportRequest(
        AssetStudioDocument document,
        AssetStudioExportProfile profile,
        AssetStudioExportOptions? options = null,
        MaterialGraph? material = null,
        AssetRecipe? asset = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        Document = document;
        Profile = profile;
        Options = options ?? new AssetStudioExportOptions();
        Material = material;
        Asset = asset;
    }

    /// <summary>
    /// Immutable document snapshot to export.
    /// </summary>
    public AssetStudioDocument Document { get; }

    /// <summary>
    /// Selected export profile.
    /// </summary>
    public AssetStudioExportProfile Profile { get; }

    /// <summary>
    /// Export options and bounds.
    /// </summary>
    public AssetStudioExportOptions Options { get; }

    /// <summary>
    /// Optional material module represented by this export.
    /// </summary>
    public MaterialGraph? Material { get; }

    /// <summary>
    /// Optional assembled asset represented by this export.
    /// </summary>
    public AssetRecipe? Asset { get; }
}
