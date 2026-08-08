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

/// <summary>Pure analysis over <see cref="CoberturaDocument"/> (no process I/O).</summary>
public static class CoverageAnalyzer
{
    /// <summary>Compute how many more hits are needed to reach <paramref name="targetPercent"/>.</summary>
    public static CoverageShortfall Shortfall(CoberturaSummary summary, double targetPercent = 95)
    {
        if (targetPercent <= 0)
            return new CoverageShortfall { TargetPercent = targetPercent };

        var rate = targetPercent / 100.0;
        var linesNeeded = summary.LinesValid <= 0
            ? 0
            : Math.Max(0, (int)Math.Ceiling(rate * summary.LinesValid) - summary.LinesCovered);
        var branchesNeeded = summary.BranchesValid <= 0
            ? 0
            : Math.Max(0, (int)Math.Ceiling(rate * summary.BranchesValid) - summary.BranchesCovered);

        return new CoverageShortfall
        {
            TargetPercent = targetPercent,
            LinesNeeded = linesNeeded,
            BranchesNeeded = branchesNeeded,
        };
    }

    /// <summary>Packages below the target on line or branch, largest branch gap first.</summary>
    public static IReadOnlyList<CoberturaPackage> PackagesBelowTarget(
        CoberturaDocument document,
        double targetPercent = 95,
        int take = 40)
    {
        return document.Packages
            .Where(p => p.LinePercent < targetPercent || (p.BranchesValid > 0 && p.BranchPercent < targetPercent))
            .OrderByDescending(p => p.BranchGap)
            .ThenByDescending(p => p.LineGap)
            .ThenBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .Take(Math.Max(1, take))
            .ToList();
    }

    /// <summary>Largest uncovered-branch packages (impact ranking).</summary>
    public static IReadOnlyList<CoberturaPackage> TopBranchGaps(
        CoberturaDocument document,
        int take = 25) =>
        document.Packages
            .Where(p => p.BranchGap > 0)
            .OrderByDescending(p => p.BranchGap)
            .ThenByDescending(p => p.LineGap)
            .Take(Math.Max(1, take))
            .ToList();

    /// <summary>Largest uncovered-line packages.</summary>
    public static IReadOnlyList<CoberturaPackage> TopLineGaps(
        CoberturaDocument document,
        int take = 25) =>
        document.Packages
            .Where(p => p.LineGap > 0)
            .OrderByDescending(p => p.LineGap)
            .ThenByDescending(p => p.BranchGap)
            .Take(Math.Max(1, take))
            .ToList();

    /// <summary>Format a markdown gap report for agents / PRs.</summary>
    public static string FormatGapsMarkdown(
        CoberturaDocument document,
        double targetPercent = 95,
        int take = 30)
    {
        var shortfall = Shortfall(document.Summary, targetPercent);
        var below = PackagesBelowTarget(document, targetPercent, take);
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("# Coverage gaps");
        sb.AppendLine();
        if (!string.IsNullOrWhiteSpace(document.SourcePath))
            sb.AppendLine($"Source: `{document.SourcePath}`");
        sb.AppendLine(
            $"**Aggregate line: {document.Summary.LinePercent.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)}%** | " +
            $"**branch: {document.Summary.BranchPercent.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)}%**");
        sb.AppendLine(
            $"Target {targetPercent.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture)}%: " +
            $"need +{shortfall.LinesNeeded} lines, +{shortfall.BranchesNeeded} branches.");
        sb.AppendLine();
        sb.AppendLine("| Package | Line % | Branch % | Line gap | Branch gap |");
        sb.AppendLine("|---------|--------|----------|----------|------------|");
        foreach (var p in below)
        {
            sb.Append("| ").Append(p.Name)
                .Append(" | ").Append(p.LinePercent.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture))
                .Append(" | ").Append(p.BranchPercent.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture))
                .Append(" | ").Append(p.LineGap)
                .Append(" | ").Append(p.BranchGap)
                .AppendLine(" |");
        }

        return sb.ToString();
    }
}

/// <summary>Evaluate aggregate line/branch against a threshold.</summary>
public static class CoverageGate
{
    /// <summary>
    /// Fail when line or branch percent is below <paramref name="failBelow"/>.
    /// Negative <paramref name="failBelow"/> disables the gate.
    /// </summary>
    public static (bool Failed, string? Message) Evaluate(CoberturaSummary summary, double failBelow)
    {
        if (failBelow <= 0)
            return (false, null);

        if (summary.LinePercent < failBelow)
        {
            return (true,
                $"Aggregate line coverage {summary.LinePercent.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)}% is below FailBelow={failBelow.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture)}%.");
        }

        if (summary.BranchesValid > 0 && summary.BranchPercent < failBelow)
        {
            return (true,
                $"Aggregate branch coverage {summary.BranchPercent.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)}% is below FailBelow={failBelow.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture)}%.");
        }

        return (false, null);
    }

    /// <summary>Evaluate a Cobertura file path.</summary>
    public static (bool Failed, string? Message) EvaluateFile(string coberturaPath, double failBelow) =>
        Evaluate(CoberturaSummaryParser.Parse(coberturaPath), failBelow);
}
