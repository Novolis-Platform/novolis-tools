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

/// <summary>One scored method.</summary>
public sealed class CrapMethodEntry
{
    /// <summary>Underlying Cobertura method.</summary>
    public required CoberturaMethod Method { get; init; }

    /// <summary>CRAP score.</summary>
    public required double Score { get; init; }

    /// <summary>True when <see cref="Score"/> exceeds the report threshold.</summary>
    public required bool Flagged { get; init; }
}

/// <summary>CRAP analysis over a Cobertura document.</summary>
public sealed class CrapReport
{
    /// <summary>Cobertura path when known.</summary>
    public string? SourcePath { get; init; }

    /// <summary>Flag threshold used.</summary>
    public required double Threshold { get; init; }

    /// <summary>All scored methods (sorted by score descending).</summary>
    public required IReadOnlyList<CrapMethodEntry> Methods { get; init; }

    /// <summary>Count of methods with score &gt; threshold.</summary>
    public int FlaggedCount => Methods.Count(m => m.Flagged);

    /// <summary>Highest CRAP among methods (0 when empty).</summary>
    public double MaxScore => Methods.Count == 0 ? 0 : Methods[0].Score;
}

/// <summary>Build and format CRAP reports from Coverlet Cobertura method rows.</summary>
public static class CrapAnalyzer
{
    /// <summary>Score every method in <paramref name="document"/> (skips <c>.cctor</c>).</summary>
    public static CrapReport Analyze(
        CoberturaDocument document,
        double threshold = CrapScore.DefaultThreshold)
    {
        ArgumentNullException.ThrowIfNull(document);

        var list = document.Methods
            .Where(m => !string.Equals(m.MethodName, ".cctor", StringComparison.Ordinal))
            .Select(m =>
            {
                var score = CrapScore.Compute(m.Complexity, m.LineRate);
                return new CrapMethodEntry
                {
                    Method = m,
                    Score = score,
                    Flagged = score > threshold,
                };
            })
            .OrderByDescending(e => e.Score)
            .ThenByDescending(e => e.Method.Complexity)
            .ThenBy(e => e.Method.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new CrapReport
        {
            SourcePath = document.SourcePath,
            Threshold = threshold,
            Methods = list,
        };
    }

    /// <summary>
    /// Single markdown file: summary + table.
    /// When <paramref name="flaggedOnly"/>, the table lists only flagged methods.
    /// <paramref name="tableTake"/> caps table rows (summary still uses full counts).
    /// </summary>
    public static string FormatMarkdown(
        CrapReport report,
        int tableTake = 200,
        bool flaggedOnly = false)
    {
        ArgumentNullException.ThrowIfNull(report);
        var inv = CultureInfo.InvariantCulture;
        var rowsQuery = flaggedOnly
            ? report.Methods.Where(m => m.Flagged)
            : report.Methods.AsEnumerable();
        var rows = rowsQuery.Take(Math.Max(1, tableTake)).ToList();
        var tableUniverse = flaggedOnly
            ? report.FlaggedCount
            : report.Methods.Count;

        var sb = new StringBuilder();
        sb.AppendLine("# CRAP report");
        sb.AppendLine();
        sb.AppendLine("Change Risk Anti-Patterns: `CRAP(m) = CC² × (1 − lineCoverage)³ + CC`.");
        sb.AppendLine();
        if (!string.IsNullOrWhiteSpace(report.SourcePath))
            sb.AppendLine($"Source: `{report.SourcePath}`");
        sb.AppendLine($"Threshold: **{report.Threshold.ToString("0.#", inv)}** (flag when score &gt; threshold)");
        sb.AppendLine(
            $"Methods scored: **{report.Methods.Count.ToString(inv)}** | " +
            $"Flagged: **{report.FlaggedCount.ToString(inv)}** | " +
            $"Max CRAP: **{report.MaxScore.ToString("0.##", inv)}**");
        if (flaggedOnly)
            sb.AppendLine("Table: flagged methods only.");
        sb.AppendLine();
        sb.AppendLine("| CRAP | CC | Line % | Branch % | Package | Method | File |");
        sb.AppendLine("|-----:|---:|-------:|---------:|---------|--------|------|");
        foreach (var e in rows)
        {
            var m = e.Method;
            sb.Append("| ").Append(e.Score.ToString("0.##", inv))
                .Append(" | ").Append(m.Complexity.ToString(inv))
                .Append(" | ").Append(m.LinePercent.ToString("0.0", inv))
                .Append(" | ").Append(m.BranchPercent.ToString("0.0", inv))
                .Append(" | ").Append(EscapeCell(m.PackageName))
                .Append(" | ").Append(EscapeCell(m.DisplayName))
                .Append(" | ").Append(EscapeCell(ShortFile(m.FileName)))
                .AppendLine(" |");
        }

        if (tableUniverse > rows.Count)
        {
            sb.AppendLine();
            sb.AppendLine($"_… {tableUniverse - rows.Count} more method(s) omitted (raise `--take`)._");
        }

        return sb.ToString();
    }

    /// <summary>
    /// Resolve report output path: explicit file, or <c>CRAP.md</c> under the caller's cwd.
    /// </summary>
    public static string ResolveReportPath(string? explicitPath, string? cwd = null)
    {
        var baseDir = string.IsNullOrWhiteSpace(cwd)
            ? Directory.GetCurrentDirectory()
            : Path.GetFullPath(cwd);

        if (string.IsNullOrWhiteSpace(explicitPath))
            return Path.Combine(baseDir, "CRAP.md");

        var full = Path.GetFullPath(explicitPath);
        if (Directory.Exists(full)
            || explicitPath.EndsWith(Path.DirectorySeparatorChar)
            || explicitPath.EndsWith(Path.AltDirectorySeparatorChar))
        {
            return Path.Combine(full, "CRAP.md");
        }

        return full;
    }

    /// <summary>Write <paramref name="markdown"/> to one file; creates parent directories.</summary>
    public static string WriteReport(string markdown, string? explicitPath = null, string? cwd = null)
    {
        var path = ResolveReportPath(explicitPath, cwd);
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
        File.WriteAllText(path, markdown);
        return path;
    }

    /// <summary>Fail when any method exceeds <paramref name="failAbove"/> (negative disables).</summary>
    public static (bool Failed, string? Message) EvaluateGate(CrapReport report, double failAbove)
    {
        if (failAbove < 0)
            return (false, null);

        var offenders = report.Methods.Count(m => m.Score > failAbove);
        if (offenders == 0)
            return (false, null);

        return (true,
            $"{offenders.ToString(CultureInfo.InvariantCulture)} method(s) exceed CRAP FailAbove={failAbove.ToString("0.#", CultureInfo.InvariantCulture)} (max {report.MaxScore.ToString("0.##", CultureInfo.InvariantCulture)}).");
    }

    private static string ShortFile(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return "";
        try
        {
            return Path.GetFileName(path);
        }
        catch
        {
            return path;
        }
    }

    private static string EscapeCell(string value) =>
        value.Replace("|", "\\|", StringComparison.Ordinal)
            .Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal);
}
