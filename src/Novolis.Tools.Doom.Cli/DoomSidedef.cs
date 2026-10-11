namespace Novolis.Tools.Doom;

public readonly record struct DoomSidedef(
    short TextureOffset,
    short RowOffset,
    string UpperTexture,
    string MiddleTexture,
    string LowerTexture,
    short Sector);
