namespace Novolis.Tools.Docs.Seed;

/// <summary>Layer placement hints for design.md seed content.</summary>
public static class DocsPackLayerHints
{
    /// <summary>Returns a markdown layer hint for the repository name.</summary>
    public static string Get(string repoName) =>
        repoName switch
        {
            _ when repoName.StartsWith("novolis-math", StringComparison.Ordinal) =>
                "Closed spine: **Math** (bottom). No Physics/Simulation/Raylib/Avalonia references.",
            _ when repoName.StartsWith("novolis-physics", StringComparison.Ordinal) =>
                "Closed spine: **Physics** over Math. No cameras, Raylib, or Avalonia.",
            _ when repoName.StartsWith("novolis-simulation", StringComparison.Ordinal) =>
                "Closed spine: **Simulation** over Physics/Math. Cameras and world clocks live here.",
            _ when repoName.StartsWith("novolis-gaming", StringComparison.Ordinal) =>
                "Closed spine: **Gaming** (`Novolis.Game.*`) over Simulation. No Avalonia in this layer.",
            _ when repoName.StartsWith("novolis-avalonia", StringComparison.Ordinal) =>
                "**Avalonia** layer only (`Novolis.Avalonia.*`). Sole libraries allowed to take Avalonia package refs.",
            _ when repoName.StartsWith("novolis-maui", StringComparison.Ordinal) =>
                "**MAUI** island (`Novolis.Maui.*`). Sole new libraries allowed to take Microsoft.Maui package refs (Voice.Platform.Maui grandfathered). Never Avalonia.",
            _ when repoName.StartsWith("novolis-raylib", StringComparison.Ordinal) =>
                "**Raylib** island — never references Simulation; apps wire Raylib + Simulation.",
            _ when repoName.StartsWith("novolis-documents", StringComparison.Ordinal) ||
                   repoName.StartsWith("novolis-markup", StringComparison.Ordinal) ||
                   repoName.StartsWith("novolis-manuscript", StringComparison.Ordinal) =>
                "Documents/Markup island — Avalonia/MAUI hosts may call PDF/HTML helpers; do not pull Avalonia or MAUI into these packages.",
            _ when repoName.StartsWith("novolis-modeling", StringComparison.Ordinal) =>
                "Avalonia-free renderer-neutral modeling scene documents and asset import over `Novolis.Math.Geometry`.",
            _ when repoName.StartsWith("novolis-cad", StringComparison.Ordinal) ||
                   repoName.StartsWith("novolis-ship", StringComparison.Ordinal) =>
                "CAD / ship domain DTOs and validation — Avalonia-free; UI chrome lives in `Novolis.Avalonia.*`. Generic scene graphs live in `novolis-modeling`.",
            _ when repoName.StartsWith("novolis-os", StringComparison.Ordinal) =>
                "Runtime images / appliances — not a NuGet library spine package.",
            _ when repoName.StartsWith("novolis-governance", StringComparison.Ordinal) ||
                   repoName is ".github" ||
                   repoName.StartsWith("novolis-workflows", StringComparison.Ordinal) ||
                   repoName.StartsWith("novolis-registry", StringComparison.Ordinal) ||
                   repoName.StartsWith("novolis-template", StringComparison.Ordinal) =>
                "Org / template / CI infrastructure — not a closed-spine library.",
            _ =>
                "Follow [library-boundaries](https://github.com/Novolis-Platform/novolis-governance/blob/main/docs/library-boundaries.md) for layer placement.",
        };
}
