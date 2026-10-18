using Novolis.Visual.Compile;

namespace Novolis.AssetStudio.Authoring;

/// <summary>
/// Host-neutral request for one preview target evaluation.
/// </summary>
public sealed record AssetStudioPreviewRequest
{
    /// <summary>
    /// Creates a preview request.
    /// </summary>
    public AssetStudioPreviewRequest(
        AssetStudioDocument document,
        string targetId,
        VisualIR? compilation = null,
        int width = 256,
        int height = 256,
        string? shape = null,
        string? camera = null,
        string? quality = null,
        IEnumerable<string>? slotMaterialFingerprints = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (string.IsNullOrWhiteSpace(targetId))
        {
            throw new ArgumentException(
                "A preview target identifier is required.",
                nameof(targetId));
        }

        if (width <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width));
        }

        if (height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(height));
        }

        Document = document;
        TargetId = targetId;
        Compilation = compilation;
        Width = width;
        Height = height;
        Shape = string.IsNullOrWhiteSpace(shape) ? "default" : shape.Trim();
        Camera = string.IsNullOrWhiteSpace(camera) ? "default" : camera.Trim();
        Quality = string.IsNullOrWhiteSpace(quality) ? "preview" : quality.Trim();
        SlotMaterialFingerprints = (slotMaterialFingerprints ?? [])
            .Where(fingerprint => !string.IsNullOrWhiteSpace(fingerprint))
            .Select(fingerprint => fingerprint.Trim())
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    /// <summary>
    /// Document snapshot to preview.
    /// </summary>
    public AssetStudioDocument Document { get; }

    /// <summary>
    /// Target identifier owned by the host or adapter.
    /// </summary>
    public string TargetId { get; }

    /// <summary>
    /// Optional compilation matching the document semantic revision.
    /// </summary>
    public VisualIR? Compilation { get; }

    /// <summary>
    /// Requested width.
    /// </summary>
    public int Width { get; }

    /// <summary>
    /// Requested height.
    /// </summary>
    public int Height { get; }

    /// <summary>Preview shape or assembled scene mode.</summary>
    public string Shape { get; }

    /// <summary>Stable camera preset or camera fingerprint.</summary>
    public string Camera { get; }

    /// <summary>Quality preset participating in cache identity.</summary>
    public string Quality { get; }

    /// <summary>Material fingerprints for independently lowered asset slots.</summary>
    public IReadOnlyList<string> SlotMaterialFingerprints { get; }

    /// <summary>Semantic fingerprint captured by the request.</summary>
    public string SemanticFingerprint => Document.ContextFingerprint;

    /// <summary>
    /// Complete cache identity; view state is deliberately excluded.
    /// </summary>
    public string CacheKey =>
        string.Join(
            "|",
            Document.DocumentKind,
            SemanticFingerprint,
            Document.Revision,
            TargetId,
            Width,
            Height,
            Shape,
            Camera,
            Quality,
            string.Join(",", SlotMaterialFingerprints));
}
