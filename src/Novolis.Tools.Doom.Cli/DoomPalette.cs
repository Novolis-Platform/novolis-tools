namespace Novolis.Tools.Doom;

public sealed class DoomPalette
{
    private readonly byte[] _rgb;

    private DoomPalette(byte[] rgb)
    {
        _rgb = rgb;
    }

    public static DoomPalette FromPlaypal(byte[] data)
    {
        if (data.Length < 768)
        {
            throw new InvalidDataException("PLAYPAL must contain at least one 256-color palette.");
        }

        return new DoomPalette(data[..768].ToArray());
    }

    public static DoomPalette Grayscale { get; } = CreateGrayscale();

    public void WriteRgba(byte index, Span<byte> destination, bool opaque = true)
    {
        var rgbOffset = index * 3;
        destination[0] = _rgb[rgbOffset];
        destination[1] = _rgb[rgbOffset + 1];
        destination[2] = _rgb[rgbOffset + 2];
        destination[3] = opaque ? (byte)255 : (byte)0;
    }

    private static DoomPalette CreateGrayscale()
    {
        var rgb = new byte[768];
        for (var i = 0; i < 256; i++)
        {
            rgb[i * 3] = (byte)i;
            rgb[i * 3 + 1] = (byte)i;
            rgb[i * 3 + 2] = (byte)i;
        }

        return new DoomPalette(rgb);
    }
}
