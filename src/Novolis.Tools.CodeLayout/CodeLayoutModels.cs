namespace Novolis.Tools.CodeLayout;

/// <summary>Options for a solution-wide source layout scan.</summary>
public sealed record CodeLayoutScanOptions(
    string Configuration = "Debug",
    string Platform = "AnyCPU",
    string? TargetFramework = null,
    bool IncludeGenerated = false,
    bool AllowDirectoryFallback = true);

