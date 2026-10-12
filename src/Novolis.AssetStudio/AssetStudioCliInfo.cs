namespace Novolis.AssetStudio.Cli;

/// <summary>
/// Stable metadata for CI and tool-catalog smoke tests.
/// </summary>
public static class AssetStudioCliInfo
{
    /// <summary>Installed dotnet tool command.</summary>
    public const string CommandName = "novolis-assetstudio";

    /// <summary>Comma-separated headless compilation targets.</summary>
    public const string SupportedTargets =
        "cpu,bake,path-trace,raster,planar,csharp";

    /// <summary>Comma-separated no-code workflow commands.</summary>
    public const string SupportedWorkflows =
        "new-template,inspect-parameters,validate,preview,build,bake,export,diff";
}
