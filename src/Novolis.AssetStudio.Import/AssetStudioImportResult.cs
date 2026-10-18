using System.Collections.Immutable;
using Novolis.AssetStudio.Authoring;

namespace Novolis.AssetStudio.Import;

/// <summary>
/// Result of a bounded recipe import.
/// </summary>
public sealed record AssetStudioImportResult
{
    private AssetStudioImportResult(
        AssetStudioDocument? document,
        IEnumerable<AssetStudioDiagnostic> diagnostics)
    {
        Document = document;
        Diagnostics = new AssetStudioDiagnostics(diagnostics).Items;
    }

    /// <summary>
    /// Creates a successful result.
    /// </summary>
    public static AssetStudioImportResult Succeed(
        AssetStudioDocument document,
        IEnumerable<AssetStudioDiagnostic>? diagnostics = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        return new(document, diagnostics ?? []);
    }

    /// <summary>
    /// Creates a rejected result.
    /// </summary>
    public static AssetStudioImportResult Reject(
        IEnumerable<AssetStudioDiagnostic> diagnostics)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);
        return new(null, diagnostics);
    }

    /// <summary>
    /// Imported document, or <see langword="null"/> when rejected.
    /// </summary>
    public AssetStudioDocument? Document { get; }

    /// <summary>
    /// Ordered import diagnostics.
    /// </summary>
    public ImmutableArray<AssetStudioDiagnostic> Diagnostics { get; }

    /// <summary>
    /// Indicates whether a document was imported without errors.
    /// </summary>
    public bool IsSuccess =>
        Document is not null &&
        Diagnostics.All(
            diagnostic => diagnostic.Severity !=
                AssetStudioDiagnosticSeverity.Error);
}
