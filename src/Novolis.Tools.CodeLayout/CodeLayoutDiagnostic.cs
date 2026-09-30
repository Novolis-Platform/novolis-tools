namespace Novolis.Tools.CodeLayout;

/// <summary>A diagnostic produced while reading a solution or project.</summary>
public sealed record CodeLayoutDiagnostic(
    string Code,
    string Message,
    string Severity,
    string? Path = null);
