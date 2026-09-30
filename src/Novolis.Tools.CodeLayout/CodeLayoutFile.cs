namespace Novolis.Tools.CodeLayout;

/// <summary>One source file discovered through a project compile item.</summary>
public sealed record CodeLayoutFile(
    string ProjectPath,
    string FilePath,
    string RelativePath,
    bool Generated,
    bool SharedAcrossProjects,
    IReadOnlyList<CodeLayoutType> Types,
    string? PrimaryType,
    IReadOnlyList<string> ExtraTypes,
    bool TypeLess,
    string TypeLessKind,
    bool DeletionCandidate,
    string? DeletionReason);
