using System.Buffers.Binary;
using System.Text;

namespace Novolis.Tools.Doom;

public static class DoomMapParser
{
    public static DoomMapData Parse(DoomMapLumpSet map)
    {
        ArgumentNullException.ThrowIfNull(map);
        var vertices = ParseVertices(map.Require("VERTEXES").Data);
        var linedefs = ParseLinedefs(map.Require("LINEDEFS").Data);
        var sidedefs = ParseSidedefs(map.Require("SIDEDEFS").Data);
        var sectors = ParseSectors(map.Require("SECTORS").Data);
        var things = ParseThings(map.Require("THINGS").Data);
        if (vertices.Length == 0)
        {
            throw new InvalidDataException($"Map {map.Name} contains no vertices.");
        }

        var minX = vertices.Min(v => v.X);
        var minY = vertices.Min(v => v.Y);
        var maxX = vertices.Max(v => v.X);
        var maxY = vertices.Max(v => v.Y);
        return new DoomMapData
        {
            MapName = map.Name,
            Bounds = new DoomMapBounds(minX, minY, maxX, maxY),
            Vertices = vertices,
            Linedefs = linedefs,
            Sidedefs = sidedefs,
            Sectors = sectors,
            Things = things,
        };
    }

    private static DoomVertex[] ParseVertices(byte[] data)
    {
        RequireRecordSize(data, 4, "VERTEXES");
        var result = new DoomVertex[data.Length / 4];
        for (var i = 0; i < result.Length; i++)
        {
            var offset = i * 4;
            result[i] = new DoomVertex(
                BinaryPrimitives.ReadInt16LittleEndian(data.AsSpan(offset, 2)),
                BinaryPrimitives.ReadInt16LittleEndian(data.AsSpan(offset + 2, 2)));
        }

        return result;
    }

    private static DoomLinedef[] ParseLinedefs(byte[] data)
    {
        RequireRecordSize(data, 14, "LINEDEFS");
        var result = new DoomLinedef[data.Length / 14];
        for (var i = 0; i < result.Length; i++)
        {
            var offset = i * 14;
            result[i] = new DoomLinedef(
                BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(offset, 2)),
                BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(offset + 2, 2)),
                BinaryPrimitives.ReadInt16LittleEndian(data.AsSpan(offset + 4, 2)),
                BinaryPrimitives.ReadInt16LittleEndian(data.AsSpan(offset + 6, 2)),
                BinaryPrimitives.ReadInt16LittleEndian(data.AsSpan(offset + 8, 2)),
                BinaryPrimitives.ReadInt16LittleEndian(data.AsSpan(offset + 10, 2)),
                BinaryPrimitives.ReadInt16LittleEndian(data.AsSpan(offset + 12, 2)));
        }

        return result;
    }

    private static DoomSidedef[] ParseSidedefs(byte[] data)
    {
        RequireRecordSize(data, 30, "SIDEDEFS");
        var result = new DoomSidedef[data.Length / 30];
        for (var i = 0; i < result.Length; i++)
        {
            var offset = i * 30;
            result[i] = new DoomSidedef(
                BinaryPrimitives.ReadInt16LittleEndian(data.AsSpan(offset, 2)),
                BinaryPrimitives.ReadInt16LittleEndian(data.AsSpan(offset + 2, 2)),
                ReadName(data, offset + 4),
                ReadName(data, offset + 12),
                ReadName(data, offset + 20),
                BinaryPrimitives.ReadInt16LittleEndian(data.AsSpan(offset + 28, 2)));
        }

        return result;
    }

    private static DoomSector[] ParseSectors(byte[] data)
    {
        RequireRecordSize(data, 26, "SECTORS");
        var result = new DoomSector[data.Length / 26];
        for (var i = 0; i < result.Length; i++)
        {
            var offset = i * 26;
            result[i] = new DoomSector(
                BinaryPrimitives.ReadInt16LittleEndian(data.AsSpan(offset, 2)),
                BinaryPrimitives.ReadInt16LittleEndian(data.AsSpan(offset + 2, 2)),
                ReadName(data, offset + 4),
                ReadName(data, offset + 12),
                BinaryPrimitives.ReadInt16LittleEndian(data.AsSpan(offset + 20, 2)),
                BinaryPrimitives.ReadInt16LittleEndian(data.AsSpan(offset + 22, 2)),
                BinaryPrimitives.ReadInt16LittleEndian(data.AsSpan(offset + 24, 2)));
        }

        return result;
    }

    private static DoomThing[] ParseThings(byte[] data)
    {
        RequireRecordSize(data, 10, "THINGS");
        var result = new DoomThing[data.Length / 10];
        for (var i = 0; i < result.Length; i++)
        {
            var offset = i * 10;
            result[i] = new DoomThing(
                BinaryPrimitives.ReadInt16LittleEndian(data.AsSpan(offset, 2)),
                BinaryPrimitives.ReadInt16LittleEndian(data.AsSpan(offset + 2, 2)),
                BinaryPrimitives.ReadInt16LittleEndian(data.AsSpan(offset + 4, 2)),
                BinaryPrimitives.ReadInt16LittleEndian(data.AsSpan(offset + 6, 2)),
                BinaryPrimitives.ReadInt16LittleEndian(data.AsSpan(offset + 8, 2)));
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

    private static void RequireRecordSize(byte[] data, int recordSize, string lumpName)
    {
        if (data.Length % recordSize != 0)
        {
            throw new InvalidDataException($"{lumpName} length {data.Length} is not divisible by {recordSize}.");
        }
    }
}
