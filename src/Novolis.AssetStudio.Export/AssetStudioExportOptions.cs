using Novolis.Visual.Baking;
using Novolis.Visual.Definitions;

namespace Novolis.AssetStudio.Export;

/// <summary>
/// Deterministic options shared by export profiles.
/// </summary>
public sealed record AssetStudioExportOptions
{
    /// <summary>
    /// C# namespace for code profiles.
    /// </summary>
    public string CodeNamespace { get; init; } = "Generated.Visuals";

    /// <summary>
    /// C# type name for code profiles.
    /// </summary>
    public string CodeTypeName { get; init; } = "GeneratedAsset";

    /// <summary>
    /// Bake dimensions and target.
    /// </summary>
    public VisualBakeOptions Bake { get; init; } = new();

    /// <summary>
    /// Artifact bounds.
    /// </summary>
    public AssetStudioExportLimits Limits { get; init; } =
        AssetStudioExportLimits.Default;

    /// <summary>
    /// Optional decoded image assets to include in sprite, packed, and
    /// code-only exports. Pixels are embedded; source files are never copied.
    /// </summary>
    public IReadOnlyList<VisualImageAsset> Assets { get; init; } = [];

    /// <summary>
    /// Validates all export options.
    /// </summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(CodeNamespace))
        {
            throw new ArgumentException(
                "A code namespace is required.",
                nameof(CodeNamespace));
        }

        if (string.IsNullOrWhiteSpace(CodeTypeName))
        {
            throw new ArgumentException(
                "A code type name is required.",
                nameof(CodeTypeName));
        }

        ArgumentNullException.ThrowIfNull(Bake);
        Bake.Validate();
        ArgumentNullException.ThrowIfNull(Limits);
        Limits.Validate();
        ArgumentNullException.ThrowIfNull(Assets);
        foreach (var asset in Assets)
        {
            ArgumentNullException.ThrowIfNull(asset);
            if (asset.RgbaPixels.Length > Limits.MaxArtifactBytes)
            {
                throw new ArgumentException(
                    $"Image asset '{asset.AssetId}' exceeds the artifact byte limit.",
                    nameof(Assets));
            }
        }

        var rawBakeBytes = (long)Bake.Width * Bake.Height * 4;
        if (rawBakeBytes > Limits.MaxArtifactBytes)
        {
            throw new ArgumentException(
                "Bake dimensions exceed the configured artifact byte limit.",
                nameof(Bake));
        }
    }
}
