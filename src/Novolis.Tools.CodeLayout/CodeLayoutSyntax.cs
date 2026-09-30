using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Novolis.Tools.CodeLayout;

internal static class CodeLayoutSyntax
{
    internal static CodeLayoutSyntaxAnalysis Analyze(string filePath, SourceText source)
    {
        var tree = CSharpSyntaxTree.ParseText(source, path: filePath);
        var root = tree.GetCompilationUnitRoot();
        var declarations = new List<TypeDeclarationSyntaxInfo>();
        Collect(root.Members, string.Empty, declarations);

        var types = declarations
            .GroupBy(declaration => declaration.Key, StringComparer.Ordinal)
            .Select(group =>
            {
                var first = group.First();
                return new CodeLayoutTypeGroup(
                    group.Key,
                    first.FullName,
                    first.Name,
                    first.Namespace,
                    first.Kind,
                    group.Select(item => item.Declaration).ToArray());
            })
            .ToArray();

        var hasTopLevelStatements = root.Members.OfType<GlobalStatementSyntax>().Any();
        var hasSignificantDirective = root
            .DescendantTrivia(descendIntoTrivia: true)
            .Any(trivia => trivia.IsDirective);
        var typeLessKind = types.Length == 0
            ? GetTypeLessKind(root, hasTopLevelStatements, hasSignificantDirective)
            : "none";
        var deletionCandidate = types.Length == 0
                                && !hasTopLevelStatements
                                && root.Usings.Count == 0
                                && root.Externs.Count == 0
                                && root.AttributeLists.Count == 0
                                && !hasSignificantDirective;

        return new CodeLayoutSyntaxAnalysis(
            root,
            types,
            hasTopLevelStatements,
            hasSignificantDirective,
            typeLessKind,
            deletionCandidate,
            deletionCandidate
                ? "No types, statements, usings, attributes, extern aliases, or significant preprocessor directives."
                : null);
    }

    internal static string? SelectPrimaryType(
        string filePath,
        IReadOnlyList<CodeLayoutTypeGroup> types)
    {
        if (types.Count == 0)
            return null;

        var fileName = Path.GetFileNameWithoutExtension(filePath);
        var exact = types.FirstOrDefault(type =>
            string.Equals(type.Name, fileName, StringComparison.Ordinal));
        if (exact is not null)
            return exact.Key;

        var ignoreCase = types.FirstOrDefault(type =>
            string.Equals(type.Name, fileName, StringComparison.OrdinalIgnoreCase));
        return ignoreCase?.Key ?? types[0].Key;
    }

    internal static CompilationUnitSyntax KeepTypes(
        CodeLayoutSyntaxAnalysis analysis,
        IReadOnlySet<string> keepKeys,
        bool stripFileMetadata)
    {
        var remove = analysis.Types
            .Where(type => !keepKeys.Contains(type.Key))
            .SelectMany(type => type.Declarations)
            .Cast<SyntaxNode>()
            .ToList();

        var root = remove.Count == 0
            ? analysis.Root
            : (CompilationUnitSyntax?)analysis.Root.RemoveNodes(remove, SyntaxRemoveOptions.KeepNoTrivia)
              ?? analysis.Root;

        root = RemoveEmptyNamespaces(root);
        if (!stripFileMetadata)
            return root;

        var metadata = new List<SyntaxNode>();
        metadata.AddRange(root.AttributeLists);
        metadata.AddRange(root.DescendantNodes().OfType<UsingDirectiveSyntax>()
            .Where(usingDirective => usingDirective.GlobalKeyword.IsKind(SyntaxKind.GlobalKeyword)));

        return metadata.Count == 0
            ? root
            : (CompilationUnitSyntax?)root.RemoveNodes(metadata, SyntaxRemoveOptions.KeepNoTrivia)
              ?? root;
    }

    private static string GetTypeLessKind(
        CompilationUnitSyntax root,
        bool hasTopLevelStatements,
        bool hasSignificantDirective)
    {
        if (hasTopLevelStatements)
            return "top-level-statements";
        if (root.Usings.Count > 0)
            return root.Usings.Any(usingDirective =>
                    usingDirective.GlobalKeyword.IsKind(SyntaxKind.GlobalKeyword))
                ? "using-only-global"
                : "using-only";
        if (root.AttributeLists.Count > 0)
            return "attributes-only";
        if (root.Externs.Count > 0)
            return "extern-alias-only";
        if (hasSignificantDirective)
            return "directives-only";
        if (root.Members.OfType<BaseNamespaceDeclarationSyntax>().Any())
            return "empty-namespace";
        return "empty-or-comments";
    }

    private static void Collect(
        SyntaxList<MemberDeclarationSyntax> members,
        string currentNamespace,
        ICollection<TypeDeclarationSyntaxInfo> declarations)
    {
        foreach (var member in members)
        {
            switch (member)
            {
                case BaseTypeDeclarationSyntax type:
                    Add(type, currentNamespace, declarations);
                    break;
                case DelegateDeclarationSyntax declaration:
                    Add(declaration, currentNamespace, declarations);
                    break;
                case BaseNamespaceDeclarationSyntax namespaceDeclaration:
                    Collect(
                        namespaceDeclaration.Members,
                        CombineNamespace(currentNamespace, namespaceDeclaration.Name.ToString()),
                        declarations);
                    break;
            }
        }
    }

    private static void Add(
        MemberDeclarationSyntax declaration,
        string currentNamespace,
        ICollection<TypeDeclarationSyntaxInfo> declarations)
    {
        var name = declaration switch
        {
            BaseTypeDeclarationSyntax type => type.Identifier.ValueText,
            DelegateDeclarationSyntax delegateDeclaration => delegateDeclaration.Identifier.ValueText,
            _ => string.Empty,
        };
        if (name.Length == 0)
            return;

        var fullName = currentNamespace.Length == 0
            ? name
            : currentNamespace + "." + name;
        declarations.Add(new TypeDeclarationSyntaxInfo(
            fullName,
            fullName,
            name,
            currentNamespace,
            declaration.Kind().ToString(),
            declaration));
    }

    private static string CombineNamespace(string left, string right) =>
        left.Length == 0 ? right : left + "." + right;

    private static CompilationUnitSyntax RemoveEmptyNamespaces(CompilationUnitSyntax root)
    {
        var empty = root.DescendantNodes()
            .OfType<BaseNamespaceDeclarationSyntax>()
            .Where(namespaceDeclaration => !HasType(namespaceDeclaration))
            .ToList();
        if (empty.Count == 0)
            return root;

        var emptySet = new HashSet<SyntaxNode>(empty);
        var outermost = empty
            .Where(namespaceDeclaration =>
                namespaceDeclaration.Parent is not BaseNamespaceDeclarationSyntax parent
                || !emptySet.Contains(parent))
            .Cast<SyntaxNode>()
            .ToList();
        return (CompilationUnitSyntax?)root.RemoveNodes(outermost, SyntaxRemoveOptions.KeepNoTrivia)
               ?? root;
    }

    private static bool HasType(BaseNamespaceDeclarationSyntax namespaceDeclaration) =>
        namespaceDeclaration.Members.Any(member =>
            member is BaseTypeDeclarationSyntax or DelegateDeclarationSyntax
            || member is BaseNamespaceDeclarationSyntax nested && HasType(nested));

    private sealed record TypeDeclarationSyntaxInfo(
        string Key,
        string FullName,
        string Name,
        string Namespace,
        string Kind,
        MemberDeclarationSyntax Declaration);
}
