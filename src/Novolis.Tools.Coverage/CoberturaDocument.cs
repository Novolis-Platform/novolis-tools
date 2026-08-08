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

/// <summary>One Cobertura method with Coverlet complexity and rates.</summary>
public sealed class CoberturaMethod
{
    /// <summary>Assembly / package name.</summary>
    public required string PackageName { get; init; }

    /// <summary>Fully qualified type name from Cobertura <c>class/@name</c>.</summary>
    public required string TypeName { get; init; }

    /// <summary>Method name (e.g. <c>Parse</c>, <c>.ctor</c>, <c>get_Foo</c>).</summary>
    public required string MethodName { get; init; }

    /// <summary>Cobertura signature string (e.g. <c>(string)</c>).</summary>
    public required string Signature { get; init; }

    /// <summary>Source file path from Cobertura (may be absolute).</summary>
    public string? FileName { get; init; }

    /// <summary>Cyclomatic complexity from Coverlet (<c>method/@complexity</c>).</summary>
    public int Complexity { get; init; }

    /// <summary>Line coverage rate 0–1.</summary>
    public double LineRate { get; init; }

    /// <summary>Branch coverage rate 0–1.</summary>
    public double BranchRate { get; init; }

    /// <summary>Line coverage percent 0–100.</summary>
    public double LinePercent => Math.Round(LineRate * 100.0, 1);

    /// <summary>Branch coverage percent 0–100.</summary>
    public double BranchPercent => Math.Round(BranchRate * 100.0, 1);

    /// <summary>Display name: type + method + signature.</summary>
    public string DisplayName => $"{TypeName}.{MethodName}{Signature}";
}

/// <summary>Parsed Cobertura document (root + packages).</summary>
public sealed class CoberturaDocument
{
    /// <summary>Root summary rates.</summary>
    public required CoberturaSummary Summary { get; init; }

    /// <summary>Per-package rollups.</summary>
    public required IReadOnlyList<CoberturaPackage> Packages { get; init; }

    /// <summary>Per-method rows (Coverlet includes complexity + rates).</summary>
    public IReadOnlyList<CoberturaMethod> Methods { get; init; } = [];

    /// <summary>Source path when loaded from disk.</summary>
    public string? SourcePath { get; init; }
}

/// <summary>Parse Cobertura XML into summaries and package tables.</summary>
public static class CoberturaDocumentParser
{
    private static readonly Regex ConditionCoverage = new(
        @"\((\d+)/(\d+)\)",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    /// <summary>Load a Cobertura file.</summary>
    public static CoberturaDocument Load(string coberturaPath)
    {
        var doc = XDocument.Load(coberturaPath);
        var coverage = doc.Root ?? throw new InvalidOperationException($"No root element in {coberturaPath}");
        var packages = new List<CoberturaPackage>();
        var methods = new List<CoberturaMethod>();
        var packagesEl = coverage.Element("packages");
        if (packagesEl is not null)
        {
            foreach (var pkg in packagesEl.Elements("package"))
            {
                var name = (string?)pkg.Attribute("name") ?? "";
                var linesCovered = 0;
                var linesValid = 0;
                var branchesCovered = 0;
                var branchesValid = 0;
                var classes = pkg.Element("classes");
                if (classes is not null)
                {
                    foreach (var cls in classes.Elements("class"))
                    {
                        var typeName = (string?)cls.Attribute("name") ?? "";
                        var fileName = (string?)cls.Attribute("filename");
                        var methodsEl = cls.Element("methods");
                        if (methodsEl is not null)
                        {
                            foreach (var method in methodsEl.Elements("method"))
                            {
                                methods.Add(new CoberturaMethod
                                {
                                    PackageName = name,
                                    TypeName = typeName,
                                    MethodName = (string?)method.Attribute("name") ?? "",
                                    Signature = (string?)method.Attribute("signature") ?? "()",
                                    FileName = fileName,
                                    Complexity = Math.Max(1, AttrInt(method, "complexity")),
                                    LineRate = Clamp01(AttrDouble(method, "line-rate")),
                                    BranchRate = Clamp01(AttrDouble(method, "branch-rate")),
                                });
                            }
                        }

                        var lines = cls.Element("lines");
                        if (lines is null)
                            continue;
                        foreach (var line in lines.Elements("line"))
                        {
                            linesValid++;
                            if (AttrInt(line, "hits") > 0)
                                linesCovered++;

                            if (!string.Equals((string?)line.Attribute("branch"), "true", StringComparison.OrdinalIgnoreCase))
                                continue;

                            var cond = (string?)line.Attribute("condition-coverage") ?? "";
                            var m = ConditionCoverage.Match(cond);
                            if (!m.Success)
                                continue;
                            branchesCovered += int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
                            branchesValid += int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture);
                        }
                    }
                }

                packages.Add(new CoberturaPackage
                {
                    Name = name,
                    LinesCovered = linesCovered,
                    LinesValid = linesValid,
                    BranchesCovered = branchesCovered,
                    BranchesValid = branchesValid,
                });
            }
        }

        return new CoberturaDocument
        {
            Summary = CoberturaSummaryParser.Parse(coberturaPath),
            Packages = packages,
            Methods = methods,
            SourcePath = Path.GetFullPath(coberturaPath),
        };
    }

    private static double Clamp01(double value) =>
        value < 0 ? 0 : value > 1 ? 1 : value;

    private static double AttrDouble(XElement el, string name) =>
        double.TryParse((string?)el.Attribute(name), NumberStyles.Float, CultureInfo.InvariantCulture, out var v)
            ? v
            : 0;

    private static int AttrInt(XElement el, string name) =>
        int.TryParse((string?)el.Attribute(name), NumberStyles.Integer, CultureInfo.InvariantCulture, out var v)
            ? v
            : 0;
}

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
