using System.Collections.Immutable;

namespace Novolis.AssetStudio.Export;

/// <summary>
/// Immutable export artifacts, manifest, and deterministic build report.
/// </summary>
public sealed record AssetStudioExportResult
{
    /// <summary>
    /// Creates an export result.
    /// </summary>
    public AssetStudioExportResult(
        IEnumerable<AssetStudioExportArtifact> artifacts,
        AssetStudioExportManifest manifest,
        AssetStudioBuildReport report)
    {
        ArgumentNullException.ThrowIfNull(artifacts);
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(report);
        Artifacts = artifacts
            .OrderBy(artifact => artifact.LogicalPath, StringComparer.Ordinal)
            .ToImmutableArray();
        Manifest = manifest;
        Report = report;
    }

    /// <summary>
    /// Sorted immutable artifacts.
    /// </summary>
    public ImmutableArray<AssetStudioExportArtifact> Artifacts { get; }

    /// <summary>
    /// Deterministic artifact manifest.
    /// </summary>
    public AssetStudioExportManifest Manifest { get; }

    /// <summary>
    /// Deterministic build report.
    /// </summary>
    public AssetStudioBuildReport Report { get; }

    /// <summary>
    /// Indicates whether export completed without errors.
    /// </summary>
    public bool Success => Report.Success;
}
