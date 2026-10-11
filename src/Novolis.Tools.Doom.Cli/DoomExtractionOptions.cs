namespace Novolis.Tools.Doom;

public sealed class DoomExtractionOptions
{
    public required string BaseWadPath { get; init; }

    public IReadOnlyList<string> PatchWadPaths { get; init; } = [];

    public required string MapName { get; init; }

    public required string OutputDirectory { get; init; }

    public string ContentProfile { get; init; } = "local";
}
