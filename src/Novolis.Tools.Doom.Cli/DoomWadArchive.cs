using System.Buffers.Binary;
using System.Text;

namespace Novolis.Tools.Doom;

public sealed class DoomWadArchive
{
    private DoomWadArchive(string magic, string sourcePath, IReadOnlyList<DoomWadLump> lumps)
    {
        Magic = magic;
        SourcePath = sourcePath;
        Lumps = lumps;
    }

    public string Magic { get; }

    public string SourcePath { get; }

    public IReadOnlyList<DoomWadLump> Lumps { get; }

    public static DoomWadArchive Load(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        return Parse(File.ReadAllBytes(path), Path.GetFullPath(path));
    }

    public static DoomWadArchive Parse(ReadOnlySpan<byte> bytes, string sourcePath = "<memory>")
    {
        if (bytes.Length < 12)
        {
            throw new InvalidDataException("A WAD must contain a 12-byte header.");
        }

        var magic = Encoding.ASCII.GetString(bytes[..4]);
        if (!string.Equals(magic, "IWAD", StringComparison.Ordinal)
            && !string.Equals(magic, "PWAD", StringComparison.Ordinal))
        {
            throw new InvalidDataException($"Unsupported WAD signature '{magic}'.");
        }

        var lumpCount = BinaryPrimitives.ReadInt32LittleEndian(bytes[4..8]);
        var directoryOffset = BinaryPrimitives.ReadInt32LittleEndian(bytes[8..12]);
        if (lumpCount < 0 || directoryOffset < 0)
        {
            throw new InvalidDataException("WAD header contains a negative count or offset.");
        }

        var directoryBytes = checked((long)lumpCount * 16L);
        var directoryEnd = checked((long)directoryOffset + directoryBytes);
        if (directoryEnd > bytes.Length)
        {
            throw new InvalidDataException("WAD directory extends beyond the file.");
        }

        var lumps = new List<DoomWadLump>(lumpCount);
        for (var i = 0; i < lumpCount; i++)
        {
            var entryOffset = checked(directoryOffset + i * 16);
            var dataOffset = BinaryPrimitives.ReadInt32LittleEndian(bytes[entryOffset..(entryOffset + 4)]);
            var dataLength = BinaryPrimitives.ReadInt32LittleEndian(bytes[(entryOffset + 4)..(entryOffset + 8)]);
            if (dataOffset < 0 || dataLength < 0)
            {
                throw new InvalidDataException($"WAD lump {i} has a negative offset or length.");
            }

            var dataEnd = checked((long)dataOffset + dataLength);
            if (dataEnd > bytes.Length)
            {
                throw new InvalidDataException($"WAD lump {i} extends beyond the file.");
            }

            var rawName = bytes[(entryOffset + 8)..(entryOffset + 16)];
            var name = DecodeName(rawName);
            var data = bytes.Slice(dataOffset, dataLength).ToArray();
            lumps.Add(new DoomWadLump(name, data, sourcePath, i));
        }

        return new DoomWadArchive(magic, sourcePath, lumps);
    }

    private static string DecodeName(ReadOnlySpan<byte> bytes)
    {
        var length = 0;
        while (length < bytes.Length && bytes[length] != 0)
        {
            length++;
        }

        return Encoding.ASCII.GetString(bytes[..length]).TrimEnd();
    }
}
