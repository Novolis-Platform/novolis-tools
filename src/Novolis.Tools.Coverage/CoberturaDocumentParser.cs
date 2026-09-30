using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Novolis.Tools.Coverage;

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
