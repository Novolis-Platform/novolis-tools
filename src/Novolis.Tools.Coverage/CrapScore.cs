using System.Collections.Concurrent;
using System.Globalization;
using System.Text;

namespace Novolis.Tools.Coverage;

/// <summary>
/// CRAP (Change Risk Anti-Patterns) score:
/// <c>CRAP(m) = CC² × (1 − cov)³ + CC</c> where <c>cov</c> is line coverage 0–1.
/// </summary>
public static class CrapScore
{
    /// <summary>Default threshold above which a method is flagged (Savoia/Evans convention).</summary>
    public const double DefaultThreshold = 30;

    /// <summary>Compute CRAP for a method.</summary>
    /// <param name="complexity">Cyclomatic complexity (minimum 1).</param>
    /// <param name="lineCoverage">Line coverage fraction 0–1.</param>
    public static double Compute(int complexity, double lineCoverage)
    {
        var cc = Math.Max(1, complexity);
        var cov = lineCoverage < 0 ? 0 : lineCoverage > 1 ? 1 : lineCoverage;
        var uncovered = 1.0 - cov;
        return (cc * cc * uncovered * uncovered * uncovered) + cc;
    }
}
