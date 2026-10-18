using Novolis.Visual.Compile;

namespace Novolis.AssetStudio.Authoring;

/// <summary>Truthful capability metadata for one preview target.</summary>
public sealed record AssetStudioPreviewTargetInfo(
    string TargetId,
    AssetStudioPreviewTargetKind Kind,
    VisualCapabilityStatus Status,
    string? Message = null);
