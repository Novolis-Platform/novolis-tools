using System.Buffers.Binary;
using System.Text;

namespace Novolis.Tools.Doom.Unit;

internal static class DoomFixture
{
    public static byte[] CreateWad()
    {
        var lumps = new List<(string Name, byte[] Data)>
        {
            ("PLAYPAL", CreatePalette()),
            ("PNAMES", CreatePNames()),
            ("TEXTURE1", CreateTextureDirectory()),
            ("P_START", []),
            ("PATCH1", CreatePatch()),
            ("P_END", []),
            ("F_START", []),
            ("FLAT1", Enumerable.Repeat((byte)1, 64 * 64).ToArray()),
            ("F_END", []),
            ("S_START", []),
            ("TROOA1", CreatePatch()),
            ("S_END", []),
            ("E1M1", []),
            ("THINGS", CreateThings()),
            ("LINEDEFS", CreateLinedefs()),
            ("SIDEDEFS", CreateSidedefs()),
            ("VERTEXES", CreateVertices()),
            ("SECTORS", CreateSectors()),
        };

        using var stream = new MemoryStream();
        stream.Write(Encoding.ASCII.GetBytes("IWAD"));
        stream.Write(new byte[8]);
        var offsets = new List<(int Offset, int Length)>();
        foreach (var lump in lumps)
        {
            var offset = checked((int)stream.Position);
            stream.Write(lump.Data);
            offsets.Add((offset, lump.Data.Length));
        }

        var directoryOffset = checked((int)stream.Position);
        Span<byte> entry = stackalloc byte[16];
        for (var i = 0; i < lumps.Count; i++)
        {
            entry.Clear();
            BinaryPrimitives.WriteInt32LittleEndian(entry[..4], offsets[i].Offset);
            BinaryPrimitives.WriteInt32LittleEndian(entry[4..8], offsets[i].Length);
            Encoding.ASCII.GetBytes(lumps[i].Name).CopyTo(entry[8..]);
            stream.Write(entry);
        }

        stream.Position = 4;
        Span<byte> header = stackalloc byte[8];
        BinaryPrimitives.WriteInt32LittleEndian(header[..4], lumps.Count);
        BinaryPrimitives.WriteInt32LittleEndian(header[4..], directoryOffset);
        stream.Write(header);
        return stream.ToArray();
    }

    private static byte[] CreatePalette()
    {
        var data = new byte[768];
        data[3] = 255;
        data[4] = 255;
        data[5] = 255;
        for (var i = 2; i < 256; i++)
        {
            data[i * 3] = (byte)i;
            data[i * 3 + 1] = (byte)i;
            data[i * 3 + 2] = (byte)i;
        }

        return data;
    }

    private static byte[] CreatePNames()
    {
        var data = new byte[12];
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(0, 4), 1);
        Encoding.ASCII.GetBytes("PATCH1").CopyTo(data.AsSpan(4, 8));
        return data;
    }

    private static byte[] CreateTextureDirectory()
    {
        var data = new byte[48];
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(0, 4), 1);
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(4, 4), 8);
        Encoding.ASCII.GetBytes("TEX1").CopyTo(data.AsSpan(8, 8));
        BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(20, 2), 4);
        BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(22, 2), 4);
        BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(28, 2), 1);
        BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(34, 2), 0);
        return data;
    }

    private static byte[] CreatePatch()
    {
        var data = new byte[60];
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(0, 2), 4);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(2, 2), 4);
        for (var column = 0; column < 4; column++)
        {
            BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(8 + column * 4, 4), 24 + column * 9);
            var offset = 24 + column * 9;
            data[offset] = 0;
            data[offset + 1] = 4;
            data[offset + 2] = 0;
            for (var row = 0; row < 4; row++)
            {
                data[offset + 3 + row] = (byte)(column + row + 2);
            }

            data[offset + 7] = 0;
            data[offset + 8] = 0xFF;
        }

        return data;
    }

    private static byte[] CreateThings()
    {
        var data = new byte[20];
        WriteThing(data.AsSpan(0, 10), 32, 32, 1);
        WriteThing(data.AsSpan(10, 10), 48, 48, 3001);
        return data;
    }

    private static void WriteThing(Span<byte> destination, short x, short y, short type)
    {
        BinaryPrimitives.WriteInt16LittleEndian(destination, x);
        BinaryPrimitives.WriteInt16LittleEndian(destination[2..], y);
        BinaryPrimitives.WriteInt16LittleEndian(destination[4..], 0);
        BinaryPrimitives.WriteInt16LittleEndian(destination[6..], type);
        BinaryPrimitives.WriteInt16LittleEndian(destination[8..], 7);
    }

    private static byte[] CreateLinedefs()
    {
        var data = new byte[56];
        for (var i = 0; i < 4; i++)
        {
            var offset = i * 14;
            BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(offset, 2), (ushort)i);
            BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(offset + 2, 2), (ushort)((i + 1) % 4));
            BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(offset + 10, 2), (short)i);
            BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(offset + 12, 2), -1);
        }

        return data;
    }

    private static byte[] CreateSidedefs()
    {
        var data = new byte[120];
        for (var i = 0; i < 4; i++)
        {
            var offset = i * 30;
            Encoding.ASCII.GetBytes("TEX1").CopyTo(data.AsSpan(offset + 20, 8));
            BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(offset + 28, 2), 0);
        }

        return data;
    }

    private static byte[] CreateVertices()
    {
        var data = new byte[16];
        WriteVertex(data.AsSpan(0, 4), 0, 0);
        WriteVertex(data.AsSpan(4, 4), 64, 0);
        WriteVertex(data.AsSpan(8, 4), 64, 64);
        WriteVertex(data.AsSpan(12, 4), 0, 64);
        return data;
    }

    private static void WriteVertex(Span<byte> destination, short x, short y)
    {
        BinaryPrimitives.WriteInt16LittleEndian(destination, x);
        BinaryPrimitives.WriteInt16LittleEndian(destination[2..], y);
    }

    private static byte[] CreateSectors()
    {
        var data = new byte[26];
        BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(2, 2), 128);
        Encoding.ASCII.GetBytes("FLAT1").CopyTo(data.AsSpan(4, 8));
        Encoding.ASCII.GetBytes("FLAT1").CopyTo(data.AsSpan(12, 8));
        BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(20, 2), 160);
        return data;
    }
}
