using System.Buffers.Binary;
using System.Text;

namespace Novolis.Tools.Doom;

public static class DoomTextureParser
{
    public static IReadOnlyList<string> ParsePatchNames(byte[] data)
    {
        if (data.Length < 4)
        {
            throw new InvalidDataException("PNAMES is truncated.");
        }

        var count = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(0, 4));
        if (count < 0 || 4L + count * 8L > data.Length)
        {
            throw new InvalidDataException("PNAMES contains an invalid count.");
        }

        var names = new List<string>(count);
        for (var i = 0; i < count; i++)
        {
            names.Add(ReadName(data, 4 + i * 8));
        }

        return names;
    }

    public static IReadOnlyList<DoomTextureDefinition> ParseTextures(byte[] data)
    {
        if (data.Length < 4)
        {
            throw new InvalidDataException("Texture directory is truncated.");
        }

        var count = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(0, 4));
        if (count < 0 || 4L + count * 4L > data.Length)
        {
            throw new InvalidDataException("Texture directory contains an invalid count.");
        }

        var result = new List<DoomTextureDefinition>(count);
        for (var i = 0; i < count; i++)
        {
            var textureOffset = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(4 + i * 4, 4));
            if (textureOffset < 0 || textureOffset > data.Length - 22)
            {
                throw new InvalidDataException($"Texture {i} has an invalid offset.");
            }

            var width = BinaryPrimitives.ReadInt16LittleEndian(data.AsSpan(textureOffset + 12, 2));
            var height = BinaryPrimitives.ReadInt16LittleEndian(data.AsSpan(textureOffset + 14, 2));
            var patchCount = BinaryPrimitives.ReadInt16LittleEndian(data.AsSpan(textureOffset + 20, 2));
            if (width <= 0 || height <= 0 || patchCount < 0)
            {
                throw new InvalidDataException($"Texture {i} has invalid dimensions or patch count.");
            }

            var patchBytes = checked((long)textureOffset + 22L + patchCount * 10L);
            if (patchBytes > data.Length)
            {
                throw new InvalidDataException($"Texture {i} patch list is truncated.");
            }

            var patches = new DoomPatchPlacement[patchCount];
            for (var p = 0; p < patchCount; p++)
            {
                var patchOffset = textureOffset + 22 + p * 10;
                patches[p] = new DoomPatchPlacement(
                    BinaryPrimitives.ReadInt16LittleEndian(data.AsSpan(patchOffset, 2)),
                    BinaryPrimitives.ReadInt16LittleEndian(data.AsSpan(patchOffset + 2, 2)),
                    BinaryPrimitives.ReadInt16LittleEndian(data.AsSpan(patchOffset + 4, 2)));
            }

            result.Add(new DoomTextureDefinition
            {
                Name = ReadName(data, textureOffset),
                Width = width,
                Height = height,
                Patches = patches,
            });
        }

        return result;
    }

    private static string ReadName(byte[] data, int offset)
    {
        var slice = data.AsSpan(offset, 8);
        var length = 0;
        while (length < slice.Length && slice[length] != 0 && slice[length] != (byte)' ')
        {
            length++;
        }

        return Encoding.ASCII.GetString(slice[..length]).ToUpperInvariant();
    }
}
