namespace Novolis.Tools.Doom;

public readonly record struct DoomThing(
    short X,
    short Y,
    short Angle,
    short Type,
    short Flags);
