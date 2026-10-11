namespace Novolis.Tools.Doom;

public sealed class DoomExtractionManifest
{
    public required string MapName { get; init; }

    public required string ContentProfile { get; init; }

    public required string SourceSha256 { get; init; }

    public required string ToolVersion { get; init; }

    public required DoomAssetManifestEntry[] Assets { get; init; }
}
