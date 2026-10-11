namespace Novolis.Tools.Doom;

public sealed class DoomRgbaImage
{
    public DoomRgbaImage(int width, int height, byte[] pixels, short offsetX = 0, short offsetY = 0)
    {
        if (width <= 0 || height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width));
        }

        if (pixels.Length != checked(width * height * 4))
        {
            throw new ArgumentException("Pixel data does not match image dimensions.", nameof(pixels));
        }

        Width = width;
        Height = height;
        Pixels = pixels;
        OffsetX = offsetX;
        OffsetY = offsetY;
    }

    public int Width { get; }

    public int Height { get; }

    public byte[] Pixels { get; }

    public short OffsetX { get; }

    public short OffsetY { get; }
}
