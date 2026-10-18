using Novolis.Visual.Compile;

namespace Novolis.AssetStudio.Authoring;

/// <summary>
/// Host-neutral preview target contract. Implementations own their execution
/// context and return only immutable result data.
/// </summary>
public interface IAssetStudioPreviewTarget
{
    /// <summary>
    /// Stable target identifier.
    /// </summary>
    string TargetId { get; }

    /// <summary>
    /// Common target kind.
    /// </summary>
    AssetStudioPreviewTargetKind Kind { get; }

    /// <summary>
    /// Capability status advertised by the target before execution.
    /// </summary>
    AssetStudioPreviewTargetInfo Info =>
        new(TargetId, Kind, VisualCapabilityStatus.Exact);

    /// <summary>
    /// Evaluates one immutable preview request.
    /// </summary>
    ValueTask<AssetStudioPreviewResult> PreviewAsync(
        AssetStudioPreviewRequest request,
        CancellationToken cancellationToken = default);
}
