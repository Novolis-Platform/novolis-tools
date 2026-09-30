using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Novolis.Tools.Coverage;

/// <summary>One Cobertura package (assembly) with line/branch counts.</summary>
public sealed class CoberturaPackage
{
    /// <summary>Assembly / package name from Cobertura.</summary>
    public required string Name { get; init; }

    /// <summary>Covered lines.</summary>
    public int LinesCovered { get; init; }

    /// <summary>Coverable lines.</summary>
    public int LinesValid { get; init; }

    /// <summary>Covered branches (condition hits).</summary>
    public int BranchesCovered { get; init; }

    /// <summary>Coverable branches.</summary>
    public int BranchesValid { get; init; }

    /// <summary>Line percent 0–100.</summary>
    public double LinePercent =>
        LinesValid == 0 ? 100.0 : Math.Round(100.0 * LinesCovered / LinesValid, 1);

    /// <summary>Branch percent 0–100 (100 when no branches).</summary>
    public double BranchPercent =>
        BranchesValid == 0 ? 100.0 : Math.Round(100.0 * BranchesCovered / BranchesValid, 1);

    /// <summary>Uncovered lines.</summary>
    public int LineGap => Math.Max(0, LinesValid - LinesCovered);

    /// <summary>Uncovered branches.</summary>
    public int BranchGap => Math.Max(0, BranchesValid - BranchesCovered);

    /// <summary>
    /// How much excluding this package would move aggregate branch toward a target rate
    /// (positive = helps when under target).
    /// </summary>
    public double BranchHelpToward(double targetRate = 0.95) =>
        BranchesValid == 0 ? 0 : targetRate * BranchesValid - BranchesCovered;
}
