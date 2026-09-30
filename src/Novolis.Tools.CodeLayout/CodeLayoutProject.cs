namespace Novolis.Tools.CodeLayout;

/// <summary>One project and its source-file layout facts.</summary>
public sealed record CodeLayoutProject(
    string ProjectPath,
    string ProjectName,
    bool UsesDefaultCompileItems,
    bool UsedDirectoryFallback,
    int GeneratedFilesSkipped,
    IReadOnlyList<CodeLayoutFile> Files,
    IReadOnlyList<CodeLayoutDiagnostic> Diagnostics);
