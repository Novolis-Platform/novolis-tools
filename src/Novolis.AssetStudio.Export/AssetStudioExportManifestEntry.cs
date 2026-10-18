namespace Novolis.AssetStudio.Export;

/// <summary>
/// Deterministic manifest metadata for one export artifact.
/// </summary>
public sealed record AssetStudioExportManifestEntry
{
    /// <summary>
    /// Creates a manifest entry from an artifact.
    /// </summary>
    public AssetStudioExportManifestEntry(AssetStudioExportArtifact artifact)
    {
        ArgumentNullException.ThrowIfNull(artifact);
        LogicalPath = artifact.LogicalPath;
        ContentType = artifact.ContentType;
        Length = artifact.Length;
        Sha256 = artifact.Sha256;
    }

    /// <summary>
    /// Logical artifact path.
    /// </summary>
    public string LogicalPath { get; }

    /// <summary>
    /// Artifact content type.
    /// </summary>
    public string ContentType { get; }

    /// <summary>
    /// Artifact length.
    /// </summary>
    public long Length { get; }

    /// <summary>
    /// Artifact SHA-256 hash.
    /// </summary>
    public string Sha256 { get; }
}
