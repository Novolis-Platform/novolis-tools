namespace Novolis.Tools.CodeLayout;

/// <summary>One source type discovered in a C# file.</summary>
public sealed record CodeLayoutType(
    string FullName,
    string Name,
    string Namespace,
    string Kind,
    int DeclarationCount);
