namespace Novolis.Tools.CodeLayout;

/// <summary>Complete solution-wide source layout map.</summary>
public sealed record CodeLayoutReport(
    string SolutionPath,
    IReadOnlyList<CodeLayoutProject> Projects,
    IReadOnlyList<CodeLayoutDiagnostic> Diagnostics)
{
    /// <summary>All mapped files, including files shared by more than one project.</summary>
    public IReadOnlyList<CodeLayoutFile> Files =>
        Projects.SelectMany(project => project.Files).ToArray();

    /// <summary>Number of mapped source files.</summary>
    public int SourceFileCount => Files.Count;

    /// <summary>Number of files with more than one distinct top-level type.</summary>
    public int MultiTypeFileCount =>
        Files.Count(file => file.ExtraTypes.Count > 0);

    /// <summary>Number of mapped files without a top-level type.</summary>
    public int TypeLessFileCount =>
        Files.Count(file => file.TypeLess);

    /// <summary>Number of safe deletion candidates.</summary>
    public int DeletionCandidateCount =>
        Files.Count(file => file.DeletionCandidate);
}
