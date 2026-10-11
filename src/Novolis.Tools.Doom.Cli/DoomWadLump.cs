namespace Novolis.Tools.Doom;

public sealed class DoomWadLump
{
    public DoomWadLump(string name, byte[] data, string sourcePath, int sourceIndex)
    {
        Name = name;
        Data = data;
        SourcePath = sourcePath;
        SourceIndex = sourceIndex;
    }

    public string Name { get; }

    public byte[] Data { get; }

    public string SourcePath { get; }

    public int SourceIndex { get; }
}
