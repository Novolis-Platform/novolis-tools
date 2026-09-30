namespace Novolis.Tools.CodeLayout;

/// <summary>One file operation performed by the layout fixer.</summary>
public sealed record CodeLayoutChange(
    string Kind,
    string FilePath,
    string Detail);
