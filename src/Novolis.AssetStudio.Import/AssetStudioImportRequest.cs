namespace Novolis.AssetStudio.Import;

/// <summary>
/// Caller-owned bounded import input. The logical path is metadata only and is
/// never resolved against the host file system.
/// </summary>
public sealed record AssetStudioImportRequest
{
    /// <summary>
    /// Creates an import request.
    /// </summary>
    public AssetStudioImportRequest(
        string logicalPath,
        ReadOnlyMemory<byte> content,
        AssetStudioImportLimits? limits = null)
    {
        LogicalPath = logicalPath ?? string.Empty;
        Content = content;
        Limits = limits ?? AssetStudioImportLimits.Default;
    }

    /// <summary>
    /// Relative logical path supplied by the caller.
    /// </summary>
    public string LogicalPath { get; }

    /// <summary>
    /// Caller-owned UTF-8 content.
    /// </summary>
    public ReadOnlyMemory<byte> Content { get; }

    /// <summary>
    /// Limits to enforce.
    /// </summary>
    public AssetStudioImportLimits Limits { get; }
}
