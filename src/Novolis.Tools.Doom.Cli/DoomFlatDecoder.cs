namespace Novolis.Tools.Doom;

public static class DoomFlatDecoder
{
    public static DoomRgbaImage Decode(byte[] data, DoomPalette palette)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(palette);
        if (data.Length < 64 * 64)
        {
            throw new InvalidDataException("A Doom flat must contain at least 4096 palette indices.");
        }

        var pixels = new byte[64 * 64 * 4];
        for (var i = 0; i < 64 * 64; i++)
        {
            palette.WriteRgba(data[i], pixels.AsSpan(i * 4, 4));
        }

        return new DoomRgbaImage(64, 64, pixels);
    }
}
