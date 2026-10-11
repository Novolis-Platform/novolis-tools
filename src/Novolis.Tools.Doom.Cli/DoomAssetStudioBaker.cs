using System.Text.Json;
using System.Security.Cryptography;
using Novolis.AssetStudio.Import;
using Novolis.Visual.Baking;
using Novolis.Visual.Definitions;

namespace Novolis.Tools.Doom;

public static class DoomAssetStudioBaker
{
    public static VisualImageAsset WriteImage(
        string output,
        string kind,
        string name,
        DoomRgbaImage image,
        ICollection<DoomAssetManifestEntry> manifest)
    {
        ArgumentException.ThrowIfNullOrEmpty(output);
        ArgumentException.ThrowIfNullOrEmpty(kind);
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(manifest);

        var fileName = SanitizeName(name) + ".png";
        var relativePath = Path.Combine(kind, fileName).Replace('\\', '/');
        var encoded = DoomPngEncoder.Encode(image);
        var imported = AssetStudioImageImporter.Import(
            new AssetStudioImageImportRequest(relativePath, encoded, $"{kind}/{name}"));
        if (!imported.IsSuccess || imported.Asset is null)
        {
            var diagnostics = string.Join(
                Environment.NewLine,
                imported.Diagnostics.Select(diagnostic => $"{diagnostic.Code}: {diagnostic.Message}"));
            throw new InvalidDataException($"Asset Studio rejected {relativePath}:{Environment.NewLine}{diagnostics}");
        }

        Directory.CreateDirectory(Path.Combine(output, kind));
        File.WriteAllBytes(Path.Combine(output, relativePath), encoded);
        var asset = CreateAsset($"{kind}/{name}", image);
        manifest.Add(new DoomAssetManifestEntry
        {
            Kind = kind,
            Name = name,
            RelativePath = relativePath,
            Width = image.Width,
            Height = image.Height,
            OffsetX = image.OffsetX,
            OffsetY = image.OffsetY,
            PixelSha256 = asset.PixelSha256,
        });
        return asset;
    }

    public static void WriteSpriteAtlas(
        string output,
        IReadOnlyList<VisualImageAsset> sprites,
        ICollection<DoomAssetManifestEntry> manifest)
    {
        if (sprites.Count == 0)
        {
            return;
        }

        var atlas = new VisualSpriteAtlasPacker().Bake(
            sprites,
            new VisualSpriteAtlasOptions { MaxWidth = 2048, Padding = 2 });
        const string kind = "sprites";
        const string name = "SPRITE_ATLAS";
        var relativePath = "sprites/sprites-atlas.png";
        Directory.CreateDirectory(Path.Combine(output, kind));
        File.WriteAllBytes(Path.Combine(output, relativePath), atlas.PngBytes);
        var atlasManifest = new
        {
            width = atlas.Image.Width,
            height = atlas.Image.Height,
            frames = atlas.Frames,
        };
        var jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };
        File.WriteAllText(
            Path.Combine(output, "sprites", "sprites-atlas.json"),
            JsonSerializer.Serialize(atlasManifest, jsonOptions));
        manifest.Add(new DoomAssetManifestEntry
        {
            Kind = kind,
            Name = name,
            RelativePath = relativePath,
            Width = atlas.Image.Width,
            Height = atlas.Image.Height,
            PixelSha256 = Convert.ToHexString(SHA256.HashData(atlas.PngBytes)).ToLowerInvariant(),
        });
    }

    private static VisualImageAsset CreateAsset(string assetId, DoomRgbaImage image)
    {
        var pivotX = image.Width == 0 ? 0.5 : Math.Clamp(0.5 - image.OffsetX / (double)image.Width, 0d, 1d);
        var pivotY = image.Height == 0 ? 0.5 : Math.Clamp(0.5 - image.OffsetY / (double)image.Height, 0d, 1d);
        return new VisualImageAsset(
            assetId,
            image.Width,
            image.Height,
            VisualImageSourceFormat.Png,
            ColorSpace.Srgb,
            AlphaMode.Straight,
            image.Pixels,
            [
                new VisualSpriteFrame(
                    "frame-0000",
                    0,
                    0,
                    image.Width,
                    image.Height,
                    pivotX,
                    pivotY),
            ]);
    }

    private static string SanitizeName(string name) =>
        new(name.Select(character => char.IsLetterOrDigit(character) ? character : '_').ToArray());
}
