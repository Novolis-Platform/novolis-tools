namespace Novolis.Tools.CodeLayout;

internal sealed record GitTrackingResult(
    IReadOnlyList<CodeLayoutChange> Changes,
    IReadOnlyList<CodeLayoutDiagnostic> Diagnostics);
