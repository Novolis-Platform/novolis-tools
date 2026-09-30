using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Novolis.Tools.CodeLayout;

internal sealed record CodeLayoutSyntaxAnalysis(
    CompilationUnitSyntax Root,
    IReadOnlyList<CodeLayoutTypeGroup> Types,
    bool HasTopLevelStatements,
    bool HasSignificantDirective,
    string TypeLessKind,
    bool DeletionCandidate,
    string? DeletionReason);
