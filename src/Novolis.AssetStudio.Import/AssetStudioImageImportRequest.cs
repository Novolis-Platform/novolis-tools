using Novolis.Visual.Definitions;

namespace Novolis.AssetStudio.Import;

/// <summary>
/// Caller-owned image bytes and bounded import metadata.
/// </summary>
public sealed record AssetStudioImageImportRequest
{
    public AssetStudioImageImportRequest(
        string logicalPath,
        ReadOnlyMemory<byte> content,
        string? assetId = null,
        ColorSpace colorSpace = ColorSpace.Srgb,
        AlphaMode alphaMode = AlphaMode.Straight,
        AssetStudioImageImportLimits? limits = null)
    {
        LogicalPath = logicalPath;
        Content = content;
        AssetId = string.IsNullOrWhiteSpace(assetId)
            ? Path.GetFileNameWithoutExtension(logicalPath)
            : assetId;
        ColorSpace = colorSpace;
        AlphaMode = alphaMode;
        Limits = limits ?? AssetStudioImageImportLimits.Default;
    }

    public string LogicalPath { get; }

    public ReadOnlyMemory<byte> Content { get; }

    public string AssetId { get; }

    public ColorSpace ColorSpace { get; }

    public AlphaMode AlphaMode { get; }

    public AssetStudioImageImportLimits Limits { get; }
}
