using System.Text.Json;
using Novolis.Tools.Doom;

namespace Novolis.Tools.Doom.Unit;

public sealed class DoomExtractionTests
{
    [Test]
    public async Task ParsesClassicMapRecords()
    {
        var archive = DoomWadArchive.Parse(DoomFixture.CreateWad());
        var map = DoomMapParser.Parse(DoomWadStack.Create(archive).FindMap("E1M1"));

        await Assert.That(map.Vertices.Length).IsEqualTo(4);
        await Assert.That(map.Linedefs.Length).IsEqualTo(4);
        await Assert.That(map.Sectors.Length).IsEqualTo(1);
        await Assert.That(map.Things.Length).IsEqualTo(2);
        await Assert.That(map.Bounds.Width).IsEqualTo(64);
        await Assert.That(map.Bounds.Height).IsEqualTo(64);
    }

    [Test]
    public async Task DecodesPatchAndCompositeTexture()
    {
        var archive = DoomWadArchive.Parse(DoomFixture.CreateWad());
        var stack = DoomWadStack.Create(archive);
        var palette = DoomPalette.FromPlaypal(stack.FindLast("PLAYPAL")!.Data);
        var patchNames = DoomTextureParser.ParsePatchNames(stack.FindLast("PNAMES")!.Data);
        var patch = stack.FindLast("PATCH1")!;
        var image = DoomPatchDecoder.Decode(patch.Data, palette);
        var definition = DoomTextureParser.ParseTextures(stack.FindLast("TEXTURE1")!.Data).Single();
        var composite = DoomTextureComposer.Compose(
            definition,
            patchNames,
            new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase) { ["PATCH1"] = patch.Data },
            palette);

        await Assert.That(image.Width).IsEqualTo(4);
        await Assert.That(image.Height).IsEqualTo(4);
        await Assert.That(image.Pixels[3]).IsEqualTo((byte)255);
        await Assert.That(composite.Pixels[3]).IsEqualTo((byte)255);
    }

    [Test]
    public async Task ExtractsDeterministicPackageAndManifest()
    {
        var root = Path.Combine(Path.GetTempPath(), "novolis-doom-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var wadPath = Path.Combine(root, "fixture.wad");
            var output = Path.Combine(root, "package");
            await File.WriteAllBytesAsync(wadPath, DoomFixture.CreateWad());
            var manifest = DoomResourceExtractor.Extract(new DoomExtractionOptions
            {
                BaseWadPath = wadPath,
                MapName = "E1M1",
                OutputDirectory = output,
                ContentProfile = "freedoom",
            });

            await Assert.That(manifest.MapName).IsEqualTo("E1M1");
            await Assert.That(manifest.ContentProfile).IsEqualTo("freedoom");
            await Assert.That(manifest.Assets).IsNotEmpty();
            await Assert.That(File.Exists(Path.Combine(output, "level.json"))).IsTrue();
            await Assert.That(File.Exists(Path.Combine(output, "map-preview.png"))).IsTrue();
            await Assert.That(File.Exists(Path.Combine(output, "manifest.json"))).IsTrue();
            await Assert.That(File.Exists(Path.Combine(output, "sprites", "sprites-atlas.png"))).IsTrue();
            await Assert.That(File.Exists(Path.Combine(output, "sprites", "sprites-atlas.json"))).IsTrue();
            await Assert.That(File.ReadAllBytes(Path.Combine(output, "textures", "TEX1.png"))[..8])
                .IsEquivalentTo(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });

            var json = await File.ReadAllTextAsync(Path.Combine(output, "level.json"));
            var map = JsonSerializer.Deserialize<DoomMapData>(
                json,
                new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
            await Assert.That(map).IsNotNull();
            await Assert.That(map!.MapName).IsEqualTo("E1M1");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public async Task RejectsOutOfBoundsDirectory()
    {
        var data = new byte[12];
        data[0] = (byte)'I';
        data[1] = (byte)'W';
        data[2] = (byte)'A';
        data[3] = (byte)'D';
        BitConverter.GetBytes(1).CopyTo(data, 4);
        BitConverter.GetBytes(12).CopyTo(data, 8);

        await Assert.That(() => DoomWadArchive.Parse(data)).Throws<InvalidDataException>();
    }
}
