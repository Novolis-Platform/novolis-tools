namespace Novolis.AssetStudio.Authoring;

/// <summary>
/// Immutable convenience wrapper around <see cref="AssetStudioSessionState"/>.
/// </summary>
public sealed record AssetStudioSession
{
    /// <summary>
    /// Creates a session around a document.
    /// </summary>
    public AssetStudioSession(AssetStudioDocument document)
        : this(AssetStudioSessionState.Create(document))
    {
    }

    private AssetStudioSession(AssetStudioSessionState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        State = state;
    }

    /// <summary>
    /// Current immutable session state.
    /// </summary>
    public AssetStudioSessionState State { get; }

    /// <summary>
    /// Current immutable document.
    /// </summary>
    public AssetStudioDocument Document => State.Document;

    /// <summary>
    /// Applies a command and returns a new session.
    /// </summary>
    public AssetStudioSession Apply(IAssetStudioCommand command) =>
        new(State.Apply(command));

    /// <summary>
    /// Alias for <see cref="Apply"/>.
    /// </summary>
    public AssetStudioSession Execute(IAssetStudioCommand command) =>
        Apply(command);

    /// <summary>
    /// Returns a new session with the last command undone.
    /// </summary>
    public AssetStudioSession Undo() => new(State.Undo());

    /// <summary>
    /// Returns a new session with the last undone command reapplied.
    /// </summary>
    public AssetStudioSession Redo() => new(State.Redo());
}
