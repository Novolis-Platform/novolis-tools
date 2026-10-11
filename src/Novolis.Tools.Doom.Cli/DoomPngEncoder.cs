using System.Collections.Immutable;
using Novolis.Visual.Baking;
using Novolis.Visual.Definitions;

namespace Novolis.Tools.Doom;

public static class DoomPngEncoder
{
    public static byte[] Encode(DoomRgbaImage image)
    {
        ArgumentNullException.ThrowIfNull(image);
        var pixels = ImmutableArray.CreateBuilder<VisualRgba32>(image.Width * image.Height);
        for (var i = 0; i < image.Width * image.Height; i++)
        {
            var offset = i * 4;
            pixels.Add(new VisualRgba32(
                image.Pixels[offset],
                image.Pixels[offset + 1],
                image.Pixels[offset + 2],
                image.Pixels[offset + 3]));
        }

        return new VisualBakeImage
        {
            Width = image.Width,
            Height = image.Height,
            Pixels = pixels.ToImmutable(),
        }.ToPng();
    }

    public static void Write(string path, DoomRgbaImage image)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        ArgumentNullException.ThrowIfNull(image);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        File.WriteAllBytes(path, Encode(image));
    }
}
