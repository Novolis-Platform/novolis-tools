using System.Collections.Immutable;
using Novolis.Visual.Compile;

namespace Novolis.AssetStudio.Authoring;

/// <summary>
/// Compile output tagged with the document and request revision that produced
/// it.
/// </summary>
public sealed record AssetStudioCompileResult
{
    /// <summary>
    /// Creates a compile result.
    /// </summary>
    public AssetStudioCompileResult(
        long requestId,
        long documentRevision,
        long semanticRevision,
        VisualIR compilation,
        IEnumerable<AssetStudioDiagnostic>? diagnostics,
        bool isLatest)
    {
        if (requestId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(requestId));
        }

        if (documentRevision < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(documentRevision));
        }

        if (semanticRevision < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(semanticRevision));
        }

        ArgumentNullException.ThrowIfNull(compilation);
        RequestId = requestId;
        DocumentRevision = documentRevision;
        SemanticRevision = semanticRevision;
        Compilation = compilation;
        Diagnostics = (diagnostics ?? [])
            .OrderBy(diagnostic => diagnostic.Severity)
            .ThenBy(diagnostic => diagnostic.Code, StringComparer.Ordinal)
            .ThenBy(diagnostic => diagnostic.Message, StringComparer.Ordinal)
            .ToImmutableArray();
        IsLatest = isLatest;
    }

    /// <summary>
    /// Coordinator request identifier.
    /// </summary>
    public long RequestId { get; }

    /// <summary>
    /// Total document revision captured by the request.
    /// </summary>
    public long DocumentRevision { get; }

    /// <summary>
    /// Semantic revision captured by the request.
    /// </summary>
    public long SemanticRevision { get; }

    /// <summary>
    /// Normalized compiler output.
    /// </summary>
    public VisualIR Compilation { get; }

    /// <summary>
    /// Ordered diagnostics from compilation.
    /// </summary>
    public ImmutableArray<AssetStudioDiagnostic> Diagnostics { get; }

    /// <summary>
    /// Indicates that this result won the latest-result race.
    /// </summary>
    public bool IsLatest { get; }

    /// <summary>
    /// Indicates that a newer request was submitted before this result
    /// completed.
    /// </summary>
    public bool IsStale => !IsLatest;

    /// <summary>
    /// Indicates whether compilation succeeded without errors.
    /// </summary>
    public bool Success => Compilation.Success;

    /// <summary>
    /// Indicates whether this result matches a document's semantic revision.
    /// </summary>
    public bool IsApplicableTo(AssetStudioDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        return SemanticRevision == document.SemanticRevision;
    }
}
