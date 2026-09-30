namespace Novolis.Tools.CodeLayout;

/// <summary>Result of applying a solution-wide layout fix.</summary>
public sealed record CodeLayoutFixResult(
    CodeLayoutReport Before,
    IReadOnlyList<CodeLayoutChange> Changes,
    IReadOnlyList<CodeLayoutDiagnostic> Diagnostics)
{
    /// <summary>Number of source files created or rewritten.</summary>
    public int FilesWritten =>
        Changes.Count(change => change.Kind is "updated" or "created");

    /// <summary>Number of source files deleted.</summary>
    public int FilesDeleted =>
        Changes.Count(change => change.Kind == "deleted");
}
