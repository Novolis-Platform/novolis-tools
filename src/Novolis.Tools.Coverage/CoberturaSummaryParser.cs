using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Novolis.Tools.Coverage;

/// <summary>Parse Cobertura root rates only (fast path).</summary>
public static class CoberturaSummaryParser
{
    /// <summary>Read rates from a Cobertura file.</summary>
    public static CoberturaSummary Parse(string coberturaPath)
    {
        var doc = XDocument.Load(coberturaPath);
        var coverage = doc.Root ?? throw new InvalidOperationException($"No root element in {coberturaPath}");
        var lineRate = AttrDouble(coverage, "line-rate");
        var branchRate = AttrDouble(coverage, "branch-rate");
        return new CoberturaSummary
        {
            LinePercent = Math.Round(lineRate * 100, 1),
            BranchPercent = Math.Round(branchRate * 100, 1),
            LinesCovered = AttrInt(coverage, "lines-covered"),
            LinesValid = AttrInt(coverage, "lines-valid"),
            BranchesCovered = AttrInt(coverage, "branches-covered"),
            BranchesValid = AttrInt(coverage, "branches-valid"),
        };
    }

    private static double AttrDouble(XElement el, string name) =>
        double.TryParse((string?)el.Attribute(name), NumberStyles.Float, CultureInfo.InvariantCulture, out var v)
            ? v
            : 0;

    private static int AttrInt(XElement el, string name) =>
        int.TryParse((string?)el.Attribute(name), NumberStyles.Integer, CultureInfo.InvariantCulture, out var v)
            ? v
            : 0;
}
