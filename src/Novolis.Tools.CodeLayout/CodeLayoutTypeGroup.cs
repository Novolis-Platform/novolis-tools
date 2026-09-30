using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Novolis.Tools.CodeLayout;

internal sealed record CodeLayoutTypeGroup(
    string Key,
    string FullName,
    string Name,
    string Namespace,
    string Kind,
    IReadOnlyList<MemberDeclarationSyntax> Declarations);
