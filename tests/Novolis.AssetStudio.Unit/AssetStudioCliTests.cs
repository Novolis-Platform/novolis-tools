using Novolis.AssetStudio.Cli;

namespace Novolis.AssetStudio.Unit;

/// <summary>
/// Verifies the public metadata exposed by the AssetStudio command.
/// </summary>
public sealed class AssetStudioCliTests
{
    /// <summary>
    /// Confirms that the command advertises the supported headless targets.
    /// </summary>
    [Test]
    public async Task Cli_metadata_declares_headless_targets()
    {
        await Assert.That(AssetStudioCliInfo.CommandName)
            .IsEqualTo("novolis-assetstudio");
        await Assert.That(AssetStudioCliInfo.SupportedTargets)
            .Contains("path-trace");
        await Assert.That(AssetStudioCliInfo.SupportedTargets)
            .Contains("csharp");
        await Assert.That(AssetStudioCliInfo.SupportedWorkflows)
            .Contains("preview");
        await Assert.That(AssetStudioCliInfo.SupportedWorkflows)
            .Contains("export");
    }
}
