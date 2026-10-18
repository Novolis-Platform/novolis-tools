namespace Novolis.AssetStudio.Authoring;

/// <summary>
/// Result of an atomic authoring operation.
/// </summary>
public sealed record AssetStudioAuthoringResult
{
    internal AssetStudioAuthoringResult(
        AssetStudioAuthoringSession session,
        bool applied,
        AssetStudioDiagnostics diagnostics)
    {
        Session = session;
        Applied = applied;
        Diagnostics = diagnostics;
        RevisionBefore = session.Document.Revision;
        RevisionAfter = session.Document.Revision;
    }

    internal AssetStudioAuthoringResult(
        AssetStudioAuthoringSession session,
        bool applied,
        AssetStudioDiagnostics diagnostics,
        long revisionBefore)
    {
        Session = session;
        Applied = applied;
        Diagnostics = diagnostics;
        RevisionBefore = revisionBefore;
        RevisionAfter = session.Document.Revision;
    }

    /// <summary>The resulting immutable session.</summary>
    public AssetStudioAuthoringSession Session { get; }

    /// <summary>Whether the operation changed semantic or history state.</summary>
    public bool Applied { get; }

    /// <summary>Diagnostics explaining a rejection or warning.</summary>
    public AssetStudioDiagnostics Diagnostics { get; }

    /// <summary>Revision before the operation.</summary>
    public long RevisionBefore { get; }

    /// <summary>Revision after the operation.</summary>
    public long RevisionAfter { get; }

    /// <summary>Convenience success flag for command callers.</summary>
    public bool Succeeded => Applied && !Diagnostics.HasErrors;
}
