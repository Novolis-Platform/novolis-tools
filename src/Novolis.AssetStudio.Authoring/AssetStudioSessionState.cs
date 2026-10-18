using System.Collections.Immutable;

namespace Novolis.AssetStudio.Authoring;

/// <summary>
/// Immutable session state with semantic command history and the latest
/// applicable compile result.
/// </summary>
public sealed record AssetStudioSessionState
{
    private sealed record HistoryEntry(
        IAssetStudioCommand Command,
        AssetStudioDocument Before,
        AssetStudioDocument After);

    private AssetStudioSessionState(
        AssetStudioDocument document,
        ImmutableArray<HistoryEntry> undoHistory,
        ImmutableArray<HistoryEntry> redoHistory,
        AssetStudioCompileResult? latestCompileResult)
    {
        Document = document;
        UndoHistory = undoHistory;
        RedoHistory = redoHistory;
        LatestCompileResult = latestCompileResult;
    }

    /// <summary>
    /// Creates a new session around a document.
    /// </summary>
    public static AssetStudioSessionState Create(AssetStudioDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        return new(
            document,
            [],
            [],
            latestCompileResult: null);
    }

    /// <summary>
    /// Current immutable document.
    /// </summary>
    public AssetStudioDocument Document { get; }

    private ImmutableArray<HistoryEntry> UndoHistory { get; }

    private ImmutableArray<HistoryEntry> RedoHistory { get; }

    /// <summary>
    /// Most recent compile result that applies to the current semantic state.
    /// </summary>
    public AssetStudioCompileResult? LatestCompileResult { get; }

    /// <summary>
    /// Number of available undo operations.
    /// </summary>
    public int UndoCount => UndoHistory.Length;

    /// <summary>
    /// Number of available redo operations.
    /// </summary>
    public int RedoCount => RedoHistory.Length;

    /// <summary>
    /// Indicates whether an undo operation is available.
    /// </summary>
    public bool CanUndo => UndoHistory.Length > 0;

    /// <summary>
    /// Indicates whether a redo operation is available.
    /// </summary>
    public bool CanRedo => RedoHistory.Length > 0;

    /// <summary>
    /// Applies one command and clears redo history.
    /// </summary>
    public AssetStudioSessionState Apply(IAssetStudioCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        var nextDocument = command.Apply(Document);
        if (ReferenceEquals(nextDocument, Document))
        {
            return this;
        }

        var entry = new HistoryEntry(command, Document, nextDocument);
        return new AssetStudioSessionState(
            nextDocument,
            UndoHistory.Add(entry),
            [],
            latestCompileResult: null);
    }

    /// <summary>
    /// Undoes the last command. Revisions remain monotonic even though
    /// semantic content returns to an earlier snapshot.
    /// </summary>
    public AssetStudioSessionState Undo()
    {
        if (UndoHistory.IsEmpty)
        {
            return this;
        }

        var entry = UndoHistory[^1];
        var restored = Document.RestoreContent(entry.Before);
        var redoEntry = new HistoryEntry(entry.Command, restored, Document);
        return new AssetStudioSessionState(
            restored,
            UndoHistory.RemoveAt(UndoHistory.Length - 1),
            RedoHistory.Add(redoEntry),
            latestCompileResult: null);
    }

    /// <summary>
    /// Reapplies the most recently undone command.
    /// </summary>
    public AssetStudioSessionState Redo()
    {
        if (RedoHistory.IsEmpty)
        {
            return this;
        }

        var entry = RedoHistory[^1];
        var applied = entry.Command.Apply(Document);
        var undoEntry = new HistoryEntry(entry.Command, Document, applied);
        return new AssetStudioSessionState(
            applied,
            UndoHistory.Add(undoEntry),
            RedoHistory.RemoveAt(RedoHistory.Length - 1),
            latestCompileResult: null);
    }

    /// <summary>
    /// Stores a compile result only when it is the coordinator's latest result
    /// and it matches the current semantic revision.
    /// </summary>
    public AssetStudioSessionState WithCompileResult(
        AssetStudioCompileResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (!result.IsLatest ||
            result.SemanticRevision != Document.SemanticRevision)
        {
            return this;
        }

        return new AssetStudioSessionState(
            Document,
            UndoHistory,
            RedoHistory,
            result);
    }

    /// <summary>
    /// Replaces view state without changing semantic history or invalidating a
    /// compile result.
    /// </summary>
    public AssetStudioSessionState WithView(AssetStudioViewState view)
    {
        ArgumentNullException.ThrowIfNull(view);
        var nextDocument = Document.WithView(view);
        if (ReferenceEquals(nextDocument, Document))
        {
            return this;
        }

        return new AssetStudioSessionState(
            nextDocument,
            UndoHistory,
            RedoHistory,
            LatestCompileResult);
    }
}
