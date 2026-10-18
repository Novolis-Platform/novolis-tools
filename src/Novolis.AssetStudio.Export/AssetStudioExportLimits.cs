namespace Novolis.AssetStudio.Export;

/// <summary>
/// Bounds applied to in-memory export artifacts.
/// </summary>
public sealed record AssetStudioExportLimits
{
    /// <summary>
    /// Maximum number of returned artifacts.
    /// </summary>
    public int MaxArtifacts { get; init; } = 128;

    /// <summary>
    /// Maximum logical path length.
    /// </summary>
    public int MaxPathLength { get; init; } = 256;

    /// <summary>
    /// Maximum size of any one artifact.
    /// </summary>
    public long MaxArtifactBytes { get; init; } = 64 * 1024 * 1024;

    /// <summary>
    /// Validates configured bounds.
    /// </summary>
    public void Validate()
    {
        if (MaxArtifacts <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(MaxArtifacts));
        }

        if (MaxPathLength <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(MaxPathLength));
        }

        if (MaxArtifactBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(MaxArtifactBytes));
        }
    }

    /// <summary>
    /// Default bounded export policy.
    /// </summary>
    public static AssetStudioExportLimits Default { get; } = new();
}
