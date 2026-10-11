namespace Novolis.Tools.Doom;

public readonly record struct DoomLinedef(
    ushort StartVertex,
    ushort EndVertex,
    short Flags,
    short Special,
    short Tag,
    short RightSidedef,
    short LeftSidedef);
