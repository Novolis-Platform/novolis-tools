using System.Collections.Immutable;
using Novolis.Visual.Definitions;

namespace Novolis.AssetStudio.Authoring;

/// <summary>
/// Immutable editor view state kept separate from semantic recipe state.
/// </summary>
public sealed record AssetStudioViewState
{
    /// <summary>
    /// Creates view state at a specified revision.
    /// </summary>
    public AssetStudioViewState(
        VisualEditorViewState? editor = null,
        string? activeOutputId = null,
        IEnumerable<NodeId>? pinnedNodes = null,
        long revision = 0)
    {
        if (revision < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(revision));
        }

        Editor = editor ?? new VisualEditorViewState();
        ActiveOutputId = string.IsNullOrWhiteSpace(activeOutputId)
            ? null
            : activeOutputId;
        PinnedNodes = (pinnedNodes ?? [])
            .Where(nodeId => !nodeId.IsEmpty)
            .ToImmutableHashSet();
        Revision = revision;
    }

    /// <summary>
    /// The editor layout and selection state.
    /// </summary>
    public VisualEditorViewState Editor { get; }

    /// <summary>
    /// The currently focused output, if one is selected.
    /// </summary>
    public string? ActiveOutputId { get; }

    /// <summary>
    /// Nodes pinned by the editor.
    /// </summary>
    public ImmutableHashSet<NodeId> PinnedNodes { get; }

    /// <summary>
    /// Monotonically increasing view revision within the document.
    /// </summary>
    public long Revision { get; }

    /// <summary>
    /// Node positions projected from the editor state.
    /// </summary>
    public ImmutableSortedDictionary<NodeId, VisualPoint> NodePositions =>
        Editor.NodePositions;

    /// <summary>
    /// Selected nodes projected from the editor state.
    /// </summary>
    public ImmutableHashSet<NodeId> SelectedNodes => Editor.SelectedNodes;

    /// <summary>
    /// Editor zoom projected from the editor state.
    /// </summary>
    public double Zoom => Editor.Zoom;
}
