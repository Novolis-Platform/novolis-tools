using System.Collections.Immutable;

namespace Novolis.AssetStudio.Authoring;

/// <summary>
/// Immutable, deterministically ordered diagnostics.
/// </summary>
public sealed record AssetStudioDiagnostics
{
    /// <summary>
    /// Creates a diagnostic collection.
    /// </summary>
    public AssetStudioDiagnostics(IEnumerable<AssetStudioDiagnostic>? items = null)
    {
        Items = (items ?? [])
            .Where(item => item is not null)
            .OrderBy(item => item.Severity)
            .ThenBy(item => item.Code, StringComparer.Ordinal)
            .ThenBy(item => item.SourcePath, StringComparer.Ordinal)
            .ThenBy(item => item.NodeId)
            .ThenBy(item => item.ConnectionId)
            .ThenBy(item => item.Message, StringComparer.Ordinal)
            .ToImmutableArray();
    }

    /// <summary>
    /// Ordered diagnostic items.
    /// </summary>
    public ImmutableArray<AssetStudioDiagnostic> Items { get; }

    /// <summary>
    /// Indicates whether any diagnostic is an error.
    /// </summary>
    public bool HasErrors =>
        Items.Any(item => item.Severity == AssetStudioDiagnosticSeverity.Error);

    /// <summary>
    /// Indicates whether any diagnostic is a warning.
    /// </summary>
    public bool HasWarnings =>
        Items.Any(item => item.Severity == AssetStudioDiagnosticSeverity.Warning);

    /// <summary>
    /// Returns an empty immutable collection.
    /// </summary>
    public static AssetStudioDiagnostics Empty { get; } = new();
}
