using Novolis.AssetStudio.Cli;

namespace Novolis.AssetStudio.Unit;

public sealed class AssetStudioCliTests
{
    [Test]
    public async Task Cli_metadata_declares_headless_targets()
    {
        await Assert.That(AssetStudioCliInfo.CommandName)
            .IsEqualTo("novolis-assetstudio");
        await Assert.That(AssetStudioCliInfo.SupportedTargets)
            .Contains("path-trace");
        await Assert.That(AssetStudioCliInfo.SupportedTargets)
            .Contains("csharp");
    }
}
