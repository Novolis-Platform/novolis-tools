namespace Novolis.Tools.Doom;

public sealed class DoomTextureDefinition
{
    public required string Name { get; init; }

    public required short Width { get; init; }

    public required short Height { get; init; }

    public required DoomPatchPlacement[] Patches { get; init; }
}
