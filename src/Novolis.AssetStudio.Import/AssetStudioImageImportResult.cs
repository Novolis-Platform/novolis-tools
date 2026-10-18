using System.Collections.Immutable;
using Novolis.AssetStudio.Authoring;
using Novolis.Visual.Definitions;

namespace Novolis.AssetStudio.Import;

/// <summary>
/// Result of bounded image decoding.
/// </summary>
public sealed record AssetStudioImageImportResult
{
    private AssetStudioImageImportResult(
        VisualImageAsset? asset,
        IEnumerable<AssetStudioDiagnostic> diagnostics)
    {
        Asset = asset;
        Diagnostics = new AssetStudioDiagnostics(diagnostics).Items;
    }

    public static AssetStudioImageImportResult Succeed(
        VisualImageAsset asset,
        IEnumerable<AssetStudioDiagnostic>? diagnostics = null)
    {
        ArgumentNullException.ThrowIfNull(asset);
        return new(asset, diagnostics ?? []);
    }

    public static AssetStudioImageImportResult Reject(
        IEnumerable<AssetStudioDiagnostic> diagnostics)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);
        return new(null, diagnostics);
    }

    public VisualImageAsset? Asset { get; }

    public ImmutableArray<AssetStudioDiagnostic> Diagnostics { get; }

    public bool IsSuccess =>
        Asset is not null &&
        Diagnostics.All(
            diagnostic => diagnostic.Severity !=
                AssetStudioDiagnosticSeverity.Error);
}
