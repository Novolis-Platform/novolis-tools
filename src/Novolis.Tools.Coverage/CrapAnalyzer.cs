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

/// <summary>CRAP analysis over Cobertura method rows (platform-wide or single file).</summary>
public sealed class CrapReport
{
    /// <summary>Primary Cobertura path when a single file was used.</summary>
    public string? SourcePath { get; init; }

    /// <summary>All Cobertura inputs scored (parallel platform fan-in).</summary>
    public IReadOnlyList<string> SourcePaths { get; init; } = [];

    /// <summary><c>Novolis.Platform.slnx</c> path when platform-scoped.</summary>
    public string? PlatformSlnxPath { get; init; }

    /// <summary>Flag threshold used.</summary>
    public required double Threshold { get; init; }

    /// <summary>Degree of parallelism used for parse/score.</summary>
    public int DegreeOfParallelism { get; init; }

    /// <summary>All scored methods (sorted by score descending).</summary>
    public required IReadOnlyList<CrapMethodEntry> Methods { get; init; }

    /// <summary>Count of methods with score &gt; threshold.</summary>
    public int FlaggedCount => Methods.Count(m => m.Flagged);

    /// <summary>Highest CRAP among methods (0 when empty).</summary>
    public double MaxScore => Methods.Count == 0 ? 0 : Methods[0].Score;
}

/// <summary>Options for platform-scoped CRAP analysis.</summary>
public sealed class CrapAnalyzeOptions
{
    /// <summary>Novolis workspace root.</summary>
    public required string Root { get; init; }

    /// <summary>Path to <c>Novolis.Platform.slnx</c> (resolved when null).</summary>
    public string? PlatformSlnxPath { get; init; }

    /// <summary>Coverage output root (default <c>&lt;root&gt;/coverage</c>).</summary>
    public string? CoverageDir { get; init; }

    /// <summary>Explicit Cobertura file(s); when set, skips platform discovery.</summary>
    public IReadOnlyList<string> CoberturaPaths { get; init; } = [];

    /// <summary>Flag threshold (default 30).</summary>
    public double Threshold { get; init; } = CrapScore.DefaultThreshold;

    /// <summary>Max parallel Cobertura parses (0 → ProcessorCount − 1).</summary>
    public int MaxDegreeOfParallelism { get; init; }

    /// <summary>Extra repo excludes (with default exclude file).</summary>
    public IReadOnlyList<string> Exclude { get; init; } = [];

    /// <summary>Optional include filter.</summary>
    public IReadOnlyList<string> Include { get; init; } = [];

    /// <summary>Exclude list file (default governance coverage-excludes).</summary>
    public string? ExcludeFile { get; init; }
}

/// <summary>Build and format CRAP reports from Coverlet Cobertura method rows.</summary>
public static class CrapAnalyzer
{
    /// <summary>Score every method in <paramref name="document"/> (skips <c>.cctor</c>).</summary>
    public static CrapReport Analyze(
        CoberturaDocument document,
        double threshold = CrapScore.DefaultThreshold) =>
        AnalyzeDocuments([document], threshold, degreeOfParallelism: 1);

    /// <summary>
    /// Platform-scoped analysis: discover Cobertura under the Platform.slnx collect layout,
    /// parse/score in parallel, emit one merged method list (no per-repo report files).
    /// </summary>
    public static CrapReport AnalyzePlatform(CrapAnalyzeOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var root = Path.GetFullPath(options.Root);
        var platformSlnx = CoverageWorkspace.ResolvePlatformSlnx(root, options.PlatformSlnxPath);
        var paths = options.CoberturaPaths.Count > 0
            ? options.CoberturaPaths.Select(Path.GetFullPath).ToList()
            : DiscoverPlatformCoberturaFiles(root, platformSlnx, options).ToList();

        if (paths.Count == 0)
        {
            throw new FileNotFoundException(
                "No Cobertura.xml found for Platform.slnx. Run: novolis-coverage collect --platform");
        }

        var dop = ResolveDop(options.MaxDegreeOfParallelism);
        var report = AnalyzeFiles(paths, options.Threshold, dop);
        return new CrapReport
        {
            SourcePath = paths.Count == 1 ? paths[0] : null,
            SourcePaths = paths,
            PlatformSlnxPath = platformSlnx,
            Threshold = options.Threshold,
            DegreeOfParallelism = dop,
            Methods = report.Methods,
        };
    }

    /// <summary>Parse and score many Cobertura files in parallel; one merged ranking.</summary>
    public static CrapReport AnalyzeFiles(
        IReadOnlyList<string> coberturaPaths,
        double threshold = CrapScore.DefaultThreshold,
        int maxDegreeOfParallelism = 0)
    {
        ArgumentNullException.ThrowIfNull(coberturaPaths);
        if (coberturaPaths.Count == 0)
            throw new ArgumentException("At least one Cobertura path is required.", nameof(coberturaPaths));

        var paths = coberturaPaths.Select(Path.GetFullPath).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var dop = ResolveDop(maxDegreeOfParallelism);
        var bag = new ConcurrentBag<CrapMethodEntry>();

        Parallel.ForEach(
            paths,
            new ParallelOptions { MaxDegreeOfParallelism = dop },
            path =>
            {
                var doc = CoberturaDocumentParser.Load(path);
                foreach (var entry in ScoreMethods(doc.Methods, threshold))
                    bag.Add(entry);
            });

        return FinishReport(bag, threshold, dop, paths, platformSlnxPath: null);
    }

    /// <summary>Score already-loaded documents (parallel over documents when &gt; 1).</summary>
    public static CrapReport AnalyzeDocuments(
        IReadOnlyList<CoberturaDocument> documents,
        double threshold = CrapScore.DefaultThreshold,
        int degreeOfParallelism = 0)
    {
        ArgumentNullException.ThrowIfNull(documents);
        if (documents.Count == 0)
            throw new ArgumentException("At least one document is required.", nameof(documents));

        var dop = documents.Count == 1 ? 1 : ResolveDop(degreeOfParallelism);
        var bag = new ConcurrentBag<CrapMethodEntry>();
        if (documents.Count == 1)
        {
            foreach (var entry in ScoreMethods(documents[0].Methods, threshold))
                bag.Add(entry);
        }
        else
        {
            Parallel.ForEach(
                documents,
                new ParallelOptions { MaxDegreeOfParallelism = dop },
                doc =>
                {
                    foreach (var entry in ScoreMethods(doc.Methods, threshold))
                        bag.Add(entry);
                });
        }

        var paths = documents
            .Select(d => d.SourcePath)
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return FinishReport(bag, threshold, dop, paths, platformSlnxPath: null);
    }

    /// <summary>
    /// Cobertura inputs for Platform.slnx: prefer per-repo files under
    /// <c>&lt;coverage&gt;/report/novolis-*/Cobertura.xml</c> (parallel fan-in),
    /// else a single merged Cobertura at the coverage root.
    /// </summary>
    public static IReadOnlyList<string> DiscoverPlatformCoberturaFiles(
        string root,
        string platformSlnxPath,
        CrapAnalyzeOptions? options = null)
    {
        var coverageDir = Path.GetFullPath(
            options?.CoverageDir
            ?? Path.Combine(root, "coverage"));

        var excludeFile = options?.ExcludeFile ?? CoverageWorkspace.DefaultExcludeFile(root);
        var excludes = CoverageWorkspace.ReadExcludes(excludeFile, options?.Exclude);
        var include = CoverageWorkspace.ExpandNames(options?.Include);
        var includeSet = include.Count > 0
            ? new HashSet<string>(include, StringComparer.OrdinalIgnoreCase)
            : null;

        var platformRepos = TestHostDiscovery.DiscoverFromPlatformSlnx(
                root, platformSlnxPath, excludes, includeSet)
            .Select(r => r.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var reportDirs = new[]
        {
            Path.Combine(coverageDir, "report"),
            Path.Combine(root, "artifacts", "coverage", "report"),
        };

        foreach (var reportDir in reportDirs)
        {
            if (!Directory.Exists(reportDir))
                continue;

            var perRepo = new List<string>();
            foreach (var dir in Directory.EnumerateDirectories(reportDir))
            {
                var repoName = Path.GetFileName(dir);
                if (platformRepos.Count > 0 && !platformRepos.Contains(repoName))
                    continue;

                var cob = Path.Combine(dir, "Cobertura.xml");
                if (File.Exists(cob))
                    perRepo.Add(Path.GetFullPath(cob));
            }

            if (perRepo.Count > 0)
                return perRepo.OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToList();
        }

        var merged = new[]
        {
            Path.Combine(coverageDir, "Cobertura.xml"),
            Path.Combine(coverageDir, "report", "Cobertura.xml"),
            Path.Combine(root, "artifacts", "coverage", "Cobertura.xml"),
            Path.Combine(root, "artifacts", "coverage", "report", "Cobertura.xml"),
        };

        var hit = merged.FirstOrDefault(File.Exists);
        return hit is null ? [] : [Path.GetFullPath(hit)];
    }

    /// <summary>
    /// Single markdown file: platform summary + one method table (no per-repo sections).
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
        sb.AppendLine("Change Risk Anti-Patterns: `CRAP(m) = CC^2 * (1 - lineCoverage)^3 + CC`.");
        sb.AppendLine();
        if (!string.IsNullOrWhiteSpace(report.PlatformSlnxPath))
            sb.AppendLine($"Platform: `{report.PlatformSlnxPath}`");
        if (report.SourcePaths.Count > 1)
        {
            sb.AppendLine(
                $"Sources: **{report.SourcePaths.Count.ToString(inv)}** Cobertura file(s), " +
                $"parallelism **{report.DegreeOfParallelism.ToString(inv)}** (one merged ranking; no per-repo reports).");
        }
        else if (report.SourcePaths.Count == 1)
        {
            sb.AppendLine($"Source: `{report.SourcePaths[0]}`");
        }
        else if (!string.IsNullOrWhiteSpace(report.SourcePath))
        {
            sb.AppendLine($"Source: `{report.SourcePath}`");
        }

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

    private static IEnumerable<CrapMethodEntry> ScoreMethods(
        IEnumerable<CoberturaMethod> methods,
        double threshold)
    {
        foreach (var m in methods)
        {
            if (string.Equals(m.MethodName, ".cctor", StringComparison.Ordinal))
                continue;

            var score = CrapScore.Compute(m.Complexity, m.LineRate);
            yield return new CrapMethodEntry
            {
                Method = m,
                Score = score,
                Flagged = score > threshold,
            };
        }
    }

    private static CrapReport FinishReport(
        ConcurrentBag<CrapMethodEntry> bag,
        double threshold,
        int dop,
        IReadOnlyList<string> paths,
        string? platformSlnxPath)
    {
        var list = bag
            .OrderByDescending(e => e.Score)
            .ThenByDescending(e => e.Method.Complexity)
            .ThenBy(e => e.Method.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new CrapReport
        {
            SourcePath = paths.Count == 1 ? paths[0] : null,
            SourcePaths = paths,
            PlatformSlnxPath = platformSlnxPath,
            Threshold = threshold,
            DegreeOfParallelism = dop,
            Methods = list,
        };
    }

    private static int ResolveDop(int requested) =>
        requested > 0 ? requested : Math.Max(1, Environment.ProcessorCount - 1);

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
