namespace Novolis.Tools.Doom;

public sealed class DoomMapLumpSet
{
    public DoomMapLumpSet(string name, string sourcePath, IReadOnlyDictionary<string, DoomWadLump> lumps)
    {
        Name = name;
        SourcePath = sourcePath;
        Lumps = lumps;
    }

    public string Name { get; }

    public string SourcePath { get; }

    public IReadOnlyDictionary<string, DoomWadLump> Lumps { get; }

    public DoomWadLump Require(string name)
    {
        if (!Lumps.TryGetValue(name, out var lump))
        {
            throw new InvalidDataException($"Map {Name} is missing the {name} lump.");
        }

        return lump;
    }
}
