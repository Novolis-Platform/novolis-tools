using System.Security.Cryptography;
using System.Text.Json;
using Novolis.Visual.Definitions;

namespace Novolis.Tools.Doom;

public static class DoomResourceExtractor
{
    public static DoomExtractionManifest Extract(DoomExtractionOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (!string.Equals(options.ContentProfile, "freedoom", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(options.ContentProfile, "original-local", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                "ContentProfile must be 'freedoom' or 'original-local'.",
                nameof(options));
        }

        var stack = DoomWadStack.Load(options.BaseWadPath, options.PatchWadPaths);
        var mapLumps = stack.FindMap(options.MapName);
        var map = DoomMapParser.Parse(mapLumps);
        var palette = stack.FindLast("PLAYPAL") is { } playpal
            ? DoomPalette.FromPlaypal(playpal.Data)
            : DoomPalette.Grayscale;
        var output = Path.GetFullPath(options.OutputDirectory);
        Directory.CreateDirectory(output);

        WriteJson(Path.Combine(output, "level.json"), map);
        var assets = new List<DoomAssetManifestEntry>();
        var patchNames = ReadPatchNames(stack);
        var patchData = ReadPatchData(stack, patchNames);
        var spriteAssets = new List<VisualImageAsset>();
        ExtractPatches(output, palette, patchData, assets);
        ExtractTextures(output, palette, stack, patchNames, patchData, assets);
        ExtractFlats(output, palette, stack, assets);
        ExtractSprites(output, palette, stack, assets, spriteAssets);
        DoomAssetStudioBaker.WriteSpriteAtlas(output, spriteAssets, assets);
        DoomPreviewRenderer.Write(Path.Combine(output, "map-preview.png"), map);

        var manifest = new DoomExtractionManifest
        {
            MapName = map.MapName,
            ContentProfile = options.ContentProfile,
            SourceSha256 = ComputeSourceHash(stack),
            ToolVersion = typeof(DoomResourceExtractor).Assembly.GetName().Version?.ToString() ?? "dev",
            Assets = assets
                .OrderBy(asset => asset.Kind, StringComparer.Ordinal)
                .ThenBy(asset => asset.Name, StringComparer.Ordinal)
                .ToArray(),
        };
        WriteJson(Path.Combine(output, "manifest.json"), manifest);
        return manifest;
    }

    private static IReadOnlyList<string> ReadPatchNames(DoomWadStack stack)
    {
        var pnames = stack.FindLast("PNAMES");
        return pnames is null ? [] : DoomTextureParser.ParsePatchNames(pnames.Data);
    }

    private static IReadOnlyDictionary<string, byte[]> ReadPatchData(
        DoomWadStack stack,
        IReadOnlyList<string> patchNames)
    {
        var data = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in patchNames)
        {
            if (stack.FindLast(name) is { } lump)
            {
                data[name] = lump.Data;
            }
        }

        return data;
    }

    private static void ExtractPatches(
        string output,
        DoomPalette palette,
        IReadOnlyDictionary<string, byte[]> patchData,
        ICollection<DoomAssetManifestEntry> assets)
    {
        foreach (var pair in patchData.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase))
        {
            if (!TryDecodePatch(pair.Value, palette, out var image))
            {
                continue;
            }

            AddImage(output, "patches", pair.Key, image, assets);
        }
    }

    private static void ExtractTextures(
        string output,
        DoomPalette palette,
        DoomWadStack stack,
        IReadOnlyList<string> patchNames,
        IReadOnlyDictionary<string, byte[]> patchData,
        ICollection<DoomAssetManifestEntry> assets)
    {
        var definitions = new Dictionary<string, DoomTextureDefinition>(StringComparer.OrdinalIgnoreCase);
        foreach (var lumpName in new[] { "TEXTURE1", "TEXTURE2" })
        {
            if (stack.FindLast(lumpName) is not { } textureLump)
            {
                continue;
            }

            foreach (var definition in DoomTextureParser.ParseTextures(textureLump.Data))
            {
                definitions[definition.Name] = definition;
            }
        }

        foreach (var definition in definitions.Values.OrderBy(definition => definition.Name, StringComparer.OrdinalIgnoreCase))
        {
            var image = DoomTextureComposer.Compose(definition, patchNames, patchData, palette);
            AddImage(output, "textures", definition.Name, image, assets);
        }
    }

    private static void ExtractFlats(
        string output,
        DoomPalette palette,
        DoomWadStack stack,
        ICollection<DoomAssetManifestEntry> assets)
    {
        foreach (var lump in FindMarkedLumps(stack, ["F_START", "FF_START"], ["F_END", "FF_END"])
                     .GroupBy(lump => lump.Name, StringComparer.OrdinalIgnoreCase)
                     .Select(group => group.Last()))
        {
            if (lump.Data.Length < 64 * 64)
            {
                continue;
            }

            var image = DoomFlatDecoder.Decode(lump.Data, palette);
            AddImage(output, "flats", lump.Name, image, assets);
        }
    }

    private static void ExtractSprites(
        string output,
        DoomPalette palette,
        DoomWadStack stack,
        ICollection<DoomAssetManifestEntry> assets,
        ICollection<VisualImageAsset> spriteAssets)
    {
        foreach (var lump in FindMarkedLumps(stack, ["S_START", "SS_START"], ["S_END", "SS_END"])
                     .GroupBy(lump => lump.Name, StringComparer.OrdinalIgnoreCase)
                     .Select(group => group.Last()))
        {
            if (!TryDecodePatch(lump.Data, palette, out var image))
            {
                continue;
            }

            AddImage(output, "sprites", lump.Name, image, assets, spriteAssets);
        }
    }

    private static IEnumerable<DoomWadLump> FindMarkedLumps(
        DoomWadStack stack,
        IEnumerable<string> starts,
        IEnumerable<string> ends)
    {
        var startSet = starts.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var endSet = ends.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var active = false;
        foreach (var lump in stack.Lumps)
        {
            if (startSet.Contains(lump.Name))
            {
                active = true;
                continue;
            }

            if (endSet.Contains(lump.Name))
            {
                active = false;
                continue;
            }

            if (active && !DoomWadStack.IsMapMarker(lump.Name))
            {
                yield return lump;
            }
        }
    }

    private static bool TryDecodePatch(byte[] data, DoomPalette palette, out DoomRgbaImage image)
    {
        try
        {
            image = DoomPatchDecoder.Decode(data, palette);
            return true;
        }
        catch (InvalidDataException)
        {
            image = null!;
            return false;
        }
    }

    private static void AddImage(
        string output,
        string kind,
        string name,
        DoomRgbaImage image,
        ICollection<DoomAssetManifestEntry> assets,
        ICollection<VisualImageAsset>? visualAssets = null)
    {
        var asset = DoomAssetStudioBaker.WriteImage(output, kind, name, image, assets);
        visualAssets?.Add(asset);
    }

    private static void WriteJson<T>(string path, T value)
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };
        File.WriteAllText(path, JsonSerializer.Serialize(value, options));
    }

    private static string ComputeSourceHash(DoomWadStack stack)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var archive in stack.Archives)
        {
            hash.AppendData(File.ReadAllBytes(archive.SourcePath));
        }

        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }

}
