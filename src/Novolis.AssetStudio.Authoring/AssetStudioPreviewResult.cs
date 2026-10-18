using System.Collections.Immutable;

namespace Novolis.AssetStudio.Authoring;

/// <summary>
/// Host-neutral preview result carrying bounded in-memory payload bytes.
/// </summary>
public sealed record AssetStudioPreviewResult
{
    /// <summary>
    /// Creates a preview result.
    /// </summary>
    public AssetStudioPreviewResult(
        string targetId,
        long documentRevision,
        bool success,
        string contentType,
        IEnumerable<byte>? payload = null,
        IEnumerable<AssetStudioDiagnostic>? diagnostics = null,
        string? semanticFingerprint = null,
        AssetStudioPreviewTargetKind kind = AssetStudioPreviewTargetKind.Custom,
        bool isStale = false)
    {
        if (string.IsNullOrWhiteSpace(targetId))
        {
            throw new ArgumentException(
                "A preview target identifier is required.",
                nameof(targetId));
        }

        if (documentRevision < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(documentRevision));
        }

        if (string.IsNullOrWhiteSpace(contentType))
        {
            throw new ArgumentException(
                "A preview content type is required.",
                nameof(contentType));
        }

        TargetId = targetId;
        DocumentRevision = documentRevision;
        Success = success;
        ContentType = contentType;
        Payload = (payload ?? []).ToImmutableArray();
        Diagnostics = new AssetStudioDiagnostics(diagnostics).Items;
        SemanticFingerprint = semanticFingerprint;
        Kind = kind;
        IsStale = isStale;
    }

    /// <summary>
    /// Target identifier that produced the result.
    /// </summary>
    public string TargetId { get; }

    /// <summary>
    /// Document revision previewed.
    /// </summary>
    public long DocumentRevision { get; }

    /// <summary>
    /// Indicates whether the target completed successfully.
    /// </summary>
    public bool Success { get; }

    /// <summary>
    /// MIME-like content type for the payload.
    /// </summary>
    public string ContentType { get; }

    /// <summary>
    /// Immutable in-memory result payload.
    /// </summary>
    public ImmutableArray<byte> Payload { get; }

    /// <summary>
    /// Ordered diagnostics.
    /// </summary>
    public ImmutableArray<AssetStudioDiagnostic> Diagnostics { get; }

    /// <summary>Semantic fingerprint rendered by the target.</summary>
    public string? SemanticFingerprint { get; }

    /// <summary>Target kind that produced this result.</summary>
    public AssetStudioPreviewTargetKind Kind { get; }

    /// <summary>Indicates that the frame predates the current document.</summary>
    public bool IsStale { get; }

    /// <summary>Returns an equivalent result marked as stale.</summary>
    public AssetStudioPreviewResult MarkStale() =>
        IsStale
            ? this
            : new AssetStudioPreviewResult(
                TargetId,
                DocumentRevision,
                Success,
                ContentType,
                Payload,
                Diagnostics,
                SemanticFingerprint,
                Kind,
                isStale: true);
}
