namespace Novolis.Tools.Coverage;

/// <summary>Parsed Cobertura root rates.</summary>
public sealed class CoberturaSummary
{
    /// <summary>Line coverage percent (0–100).</summary>
    public double LinePercent { get; init; }

    /// <summary>Branch coverage percent (0–100).</summary>
    public double BranchPercent { get; init; }

    /// <summary>Covered lines.</summary>
    public int LinesCovered { get; init; }

    /// <summary>Coverable lines.</summary>
    public int LinesValid { get; init; }

    /// <summary>Covered branches.</summary>
    public int BranchesCovered { get; init; }

    /// <summary>Coverable branches.</summary>
    public int BranchesValid { get; init; }
}
