namespace Novolis.Tools.Doom;

public sealed class DoomAssetManifestEntry
{
    public required string Kind { get; init; }

    public required string Name { get; init; }

    public required string RelativePath { get; init; }

    public required int Width { get; init; }

    public required int Height { get; init; }

    public short OffsetX { get; init; }

    public short OffsetY { get; init; }

    public string? PixelSha256 { get; init; }
}
