using System.Buffers.Binary;

namespace Novolis.Tools.Doom;

public static class DoomPatchDecoder
{
    public static DoomRgbaImage Decode(byte[] data, DoomPalette palette)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(palette);
        if (data.Length < 8)
        {
            throw new InvalidDataException("Doom patch data is shorter than its header.");
        }

        var width = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(0, 2));
        var height = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(2, 2));
        var offsetX = BinaryPrimitives.ReadInt16LittleEndian(data.AsSpan(4, 2));
        var offsetY = BinaryPrimitives.ReadInt16LittleEndian(data.AsSpan(6, 2));
        if (width == 0 || height == 0)
        {
            throw new InvalidDataException("Doom patch dimensions must be positive.");
        }

        var columnTableEnd = checked(8 + width * 4);
        if (columnTableEnd > data.Length)
        {
            throw new InvalidDataException("Doom patch column table extends beyond the data.");
        }

        var pixels = new byte[checked(width * height * 4)];
        for (var x = 0; x < width; x++)
        {
            var columnOffset = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(8 + x * 4, 4));
            if (columnOffset < columnTableEnd || columnOffset >= data.Length)
            {
                throw new InvalidDataException($"Doom patch column {x} has an invalid offset.");
            }

            var cursor = columnOffset;
            while (cursor < data.Length)
            {
                var topDelta = data[cursor++];
                if (topDelta == 0xFF)
                {
                    break;
                }

                if (cursor + 2 > data.Length)
                {
                    throw new InvalidDataException("Doom patch post header is truncated.");
                }

                var length = data[cursor++];
                cursor++;
                if (cursor + length > data.Length)
                {
                    throw new InvalidDataException("Doom patch post pixels are truncated.");
                }

                for (var y = 0; y < length; y++)
                {
                    var targetY = topDelta + y;
                    if (targetY >= height)
                    {
                        continue;
                    }

                    var target = checked((targetY * width + x) * 4);
                    palette.WriteRgba(data[cursor + y], pixels.AsSpan(target, 4));
                }

                cursor += length;
                if (cursor < data.Length)
                {
                    cursor++;
                }
            }
        }

        return new DoomRgbaImage(width, height, pixels, offsetX, offsetY);
    }
}
