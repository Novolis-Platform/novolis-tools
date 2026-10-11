namespace Novolis.Tools.Doom;

public readonly record struct DoomMapBounds(short MinX, short MinY, short MaxX, short MaxY)
{
    public int Width => MaxX - MinX;

    public int Height => MaxY - MinY;
}
