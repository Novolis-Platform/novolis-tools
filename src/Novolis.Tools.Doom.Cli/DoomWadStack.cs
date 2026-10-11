namespace Novolis.Tools.Doom;

public sealed class DoomWadStack
{
    private readonly List<DoomWadLump> _lumps = [];

    private DoomWadStack(IEnumerable<DoomWadArchive> archives)
    {
        Archives = archives.ToArray();
        foreach (var archive in Archives)
        {
            _lumps.AddRange(archive.Lumps);
        }
    }

    public IReadOnlyList<DoomWadArchive> Archives { get; }

    public IReadOnlyList<DoomWadLump> Lumps => _lumps;

    public static DoomWadStack Load(string basePath, IEnumerable<string>? patchPaths = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(basePath);
        var paths = new List<string> { basePath };
        if (patchPaths is not null)
        {
            paths.AddRange(patchPaths);
        }

        return new DoomWadStack(paths.Select(DoomWadArchive.Load));
    }

    public static DoomWadStack Create(params DoomWadArchive[] archives)
    {
        ArgumentNullException.ThrowIfNull(archives);
        if (archives.Length == 0)
        {
            throw new ArgumentException("At least one archive is required.", nameof(archives));
        }

        return new DoomWadStack(archives);
    }

    public DoomWadLump? FindLast(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        for (var i = _lumps.Count - 1; i >= 0; i--)
        {
            if (string.Equals(_lumps[i].Name, name, StringComparison.OrdinalIgnoreCase))
            {
                return _lumps[i];
            }
        }

        return null;
    }

    public DoomMapLumpSet FindMap(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        var start = -1;
        for (var i = _lumps.Count - 1; i >= 0; i--)
        {
            if (string.Equals(_lumps[i].Name, name, StringComparison.OrdinalIgnoreCase)
                && IsMapMarker(_lumps[i].Name))
            {
                start = i;
                break;
            }
        }

        if (start < 0)
        {
            throw new InvalidDataException($"Map '{name}' was not found in the WAD stack.");
        }

        var mapLumps = new Dictionary<string, DoomWadLump>(StringComparer.OrdinalIgnoreCase);
        for (var i = start + 1; i < _lumps.Count && !IsMapMarker(_lumps[i].Name); i++)
        {
            mapLumps[_lumps[i].Name] = _lumps[i];
        }

        return new DoomMapLumpSet(_lumps[start].Name, _lumps[start].SourcePath, mapLumps);
    }

    public static bool IsMapMarker(string name)
    {
        if (name.Length == 4
            && name[0] is 'E' or 'e'
            && name[2] is 'M' or 'm'
            && char.IsDigit(name[1])
            && char.IsDigit(name[3]))
        {
            return true;
        }

        return name.Length == 5
            && name.StartsWith("MAP", StringComparison.OrdinalIgnoreCase)
            && char.IsDigit(name[3])
            && char.IsDigit(name[4]);
    }
}
