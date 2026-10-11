namespace Novolis.Tools.Doom;

public static class DoomPreviewRenderer
{
    public static void Write(string path, DoomMapData map, int size = 512)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        ArgumentNullException.ThrowIfNull(map);
        if (size < 32)
        {
            throw new ArgumentOutOfRangeException(nameof(size));
        }

        var pixels = new byte[checked(size * size * 4)];
        for (var i = 0; i < size * size; i++)
        {
            pixels[i * 4] = 12;
            pixels[i * 4 + 1] = 14;
            pixels[i * 4 + 2] = 20;
            pixels[i * 4 + 3] = 255;
        }

        var width = Math.Max(1, map.Bounds.Width);
        var height = Math.Max(1, map.Bounds.Height);
        var padding = size * 0.06f;
        var scale = Math.Min((size - padding * 2f) / width, (size - padding * 2f) / height);
        var transform = (short x, short y) =>
        (
            (int)MathF.Round(padding + (x - map.Bounds.MinX) * scale),
            (int)MathF.Round(size - padding - (y - map.Bounds.MinY) * scale)
        );

        foreach (var line in map.Linedefs)
        {
            if (line.StartVertex >= map.Vertices.Length || line.EndVertex >= map.Vertices.Length)
            {
                continue;
            }

            var start = transform(map.Vertices[line.StartVertex].X, map.Vertices[line.StartVertex].Y);
            var end = transform(map.Vertices[line.EndVertex].X, map.Vertices[line.EndVertex].Y);
            DrawLine(pixels, size, start.Item1, start.Item2, end.Item1, end.Item2, 112, 126, 158);
        }

        foreach (var thing in map.Things)
        {
            var point = transform(thing.X, thing.Y);
            var color = thing.Type is 1 or 2 or 3 or 4
                ? ((byte)90, (byte)220, (byte)120)
                : thing.Type >= 3000
                    ? ((byte)220, (byte)92, (byte)82)
                    : ((byte)230, (byte)190, (byte)80);
            DrawDot(pixels, size, point.Item1, point.Item2, color.Item1, color.Item2, color.Item3);
        }

        DoomPngEncoder.Write(path, new DoomRgbaImage(size, size, pixels));
    }

    private static void DrawLine(byte[] pixels, int size, int x0, int y0, int x1, int y1, byte r, byte g, byte b)
    {
        var dx = Math.Abs(x1 - x0);
        var sx = x0 < x1 ? 1 : -1;
        var dy = -Math.Abs(y1 - y0);
        var sy = y0 < y1 ? 1 : -1;
        var error = dx + dy;
        while (true)
        {
            PutPixel(pixels, size, x0, y0, r, g, b);
            if (x0 == x1 && y0 == y1)
            {
                return;
            }

            var twice = 2 * error;
            if (twice >= dy)
            {
                error += dy;
                x0 += sx;
            }

            if (twice <= dx)
            {
                error += dx;
                y0 += sy;
            }
        }
    }

    private static void DrawDot(byte[] pixels, int size, int x, int y, byte r, byte g, byte b)
    {
        for (var offsetY = -2; offsetY <= 2; offsetY++)
        {
            for (var offsetX = -2; offsetX <= 2; offsetX++)
            {
                PutPixel(pixels, size, x + offsetX, y + offsetY, r, g, b);
            }
        }
    }

    private static void PutPixel(byte[] pixels, int size, int x, int y, byte r, byte g, byte b)
    {
        if ((uint)x >= (uint)size || (uint)y >= (uint)size)
        {
            return;
        }

        var offset = (y * size + x) * 4;
        pixels[offset] = r;
        pixels[offset + 1] = g;
        pixels[offset + 2] = b;
        pixels[offset + 3] = 255;
    }
}
