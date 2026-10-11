namespace Novolis.Tools.Doom;

public readonly record struct DoomSector(
    short FloorHeight,
    short CeilingHeight,
    string FloorFlat,
    string CeilingFlat,
    short LightLevel,
    short Special,
    short Tag);
