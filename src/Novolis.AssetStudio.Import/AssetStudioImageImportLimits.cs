namespace Novolis.AssetStudio.Import;

/// <summary>
/// Resource limits applied before and during decoded image import.
/// </summary>
public sealed record AssetStudioImageImportLimits
{
    public static AssetStudioImageImportLimits Default { get; } = new();

    public int MaxPathLength { get; init; } = 512;

    public long MaxPayloadBytes { get; init; } = 64L * 1024 * 1024;

    public long MaxDecodedPixels { get; init; } = 16_777_216;

    public int MaxDimension { get; init; } = 16_384;

    public int MaxFrames { get; init; } = 512;

    public void Validate()
    {
        if (MaxPathLength <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(MaxPathLength));
        }

        if (MaxPayloadBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(MaxPayloadBytes));
        }

        if (MaxDecodedPixels <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(MaxDecodedPixels));
        }

        if (MaxDimension <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(MaxDimension));
        }

        if (MaxFrames <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(MaxFrames));
        }
    }
}
