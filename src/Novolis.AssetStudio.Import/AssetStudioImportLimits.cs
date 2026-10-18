namespace Novolis.AssetStudio.Import;

/// <summary>
/// Bounds applied before parsing imported content.
/// </summary>
public sealed record AssetStudioImportLimits
{
    /// <summary>
    /// Maximum UTF-8 payload size.
    /// </summary>
    public long MaxPayloadBytes { get; init; } = 4 * 1024 * 1024;

    /// <summary>
    /// Maximum logical path length.
    /// </summary>
    public int MaxPathLength { get; init; } = 256;

    /// <summary>
    /// Maximum JSON nesting depth.
    /// </summary>
    public int MaxJsonDepth { get; init; } = 64;

    /// <summary>
    /// Maximum node count.
    /// </summary>
    public int MaxNodes { get; init; } = 10_000;

    /// <summary>
    /// Maximum connection count.
    /// </summary>
    public int MaxConnections { get; init; } = 20_000;

    /// <summary>
    /// Maximum parameter count.
    /// </summary>
    public int MaxParameters { get; init; } = 10_000;

    /// <summary>
    /// Maximum output count.
    /// </summary>
    public int MaxOutputs { get; init; } = 1_000;

    /// <summary>
    /// Validates all configured bounds.
    /// </summary>
    public void Validate()
    {
        if (MaxPayloadBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(MaxPayloadBytes));
        }

        if (MaxPathLength <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(MaxPathLength));
        }

        if (MaxJsonDepth <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(MaxJsonDepth));
        }

        if (MaxNodes < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(MaxNodes));
        }

        if (MaxConnections < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(MaxConnections));
        }

        if (MaxParameters < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(MaxParameters));
        }

        if (MaxOutputs < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(MaxOutputs));
        }
    }

    /// <summary>
    /// Default bounded import policy.
    /// </summary>
    public static AssetStudioImportLimits Default { get; } = new();
}
