namespace Novolis.Tools.Coverage;

/// <summary>Shortfall against a line/branch percent gate.</summary>
public sealed class CoverageShortfall
{
    /// <summary>Target percent (e.g. 95).</summary>
    public required double TargetPercent { get; init; }

    /// <summary>Additional covered lines needed (0 if at/above target).</summary>
    public int LinesNeeded { get; init; }

    /// <summary>Additional covered branches needed.</summary>
    public int BranchesNeeded { get; init; }

    /// <summary>True when both metrics meet the target.</summary>
    public bool MeetsTarget => LinesNeeded == 0 && BranchesNeeded == 0;
}
