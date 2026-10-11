namespace Novolis.Tools.Doom;

public sealed class DoomMapData
{
    public required string MapName { get; init; }

    public required DoomMapBounds Bounds { get; init; }

    public required DoomVertex[] Vertices { get; init; }

    public required DoomLinedef[] Linedefs { get; init; }

    public required DoomSidedef[] Sidedefs { get; init; }

    public required DoomSector[] Sectors { get; init; }

    public required DoomThing[] Things { get; init; }
}
