using System.Collections.Immutable;
using System.Security.Cryptography;

namespace Novolis.AssetStudio.Export;

/// <summary>
/// One bounded in-memory export artifact addressed by a logical path.
/// </summary>
public sealed record AssetStudioExportArtifact
{
    /// <summary>
    /// Creates and validates an artifact.
    /// </summary>
    public AssetStudioExportArtifact(
        string logicalPath,
        string contentType,
        ReadOnlyMemory<byte> content)
    {
        if (!TryValidatePath(logicalPath, out var pathMessage))
        {
            throw new ArgumentException(pathMessage, nameof(logicalPath));
        }

        if (string.IsNullOrWhiteSpace(contentType))
        {
            throw new ArgumentException(
                "An artifact content type is required.",
                nameof(contentType));
        }

        LogicalPath = logicalPath.Replace('\\', '/');
        ContentType = contentType;
        Content = ImmutableArray.CreateRange<byte>(content.ToArray());
        Sha256 = Convert.ToHexString(
                SHA256.HashData(Content.AsSpan()))
            .ToLowerInvariant();
    }

    /// <summary>
    /// Relative logical artifact path.
    /// </summary>
    public string LogicalPath { get; }

    /// <summary>
    /// MIME-like content type.
    /// </summary>
    public string ContentType { get; }

    /// <summary>
    /// Immutable artifact bytes.
    /// </summary>
    public ImmutableArray<byte> Content { get; }

    /// <summary>
    /// Lowercase SHA-256 content hash.
    /// </summary>
    public string Sha256 { get; }

    /// <summary>
    /// Artifact byte length.
    /// </summary>
    public long Length => Content.Length;

    private static bool TryValidatePath(string path, out string message)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            message = "An artifact logical path is required.";
            return false;
        }

        if (path.Contains('\0') ||
            path.StartsWith("/", StringComparison.Ordinal) ||
            path.StartsWith("\\", StringComparison.Ordinal) ||
            (path.Length >= 2 && path[1] == ':'))
        {
            message = "An artifact path must be relative.";
            return false;
        }

        var segments = path
            .Replace('\\', '/')
            .Split('/', StringSplitOptions.None);
        if (segments.Any(segment =>
                segment.Length == 0 ||
                segment is "." or ".." ||
                segment.Contains(':')))
        {
            message = "An artifact path contains an unsafe segment.";
            return false;
        }

        message = string.Empty;
        return true;
    }
}
