using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Novolis.Tools.Coverage;

/// <summary>Static scan for repos without test hosts and assemblies never referenced by a test project.</summary>
public static class TestGapScanner
{
    private static readonly Regex ProjectReferenceInclude = new(
        @"ProjectReference\s+Include=""([^""]+)""",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex SkipProductionPath = new(
        @"[\\/](tests|samples|benchmarks|tools|artifacts|obj|bin)[\\/]",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    /// <summary>Scan workspace repos for missing test hosts and untested production assemblies.</summary>
    public static TestGapReport Scan(TestGapOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var root = CoverageWorkspace.ResolveRoot(options.Root);
        var excludeFile = string.IsNullOrWhiteSpace(options.ExcludeFile)
            ? CoverageWorkspace.DefaultExcludeFile(root)
            : options.ExcludeFile;
        var exclude = CoverageWorkspace.ReadExcludes(excludeFile, options.Exclude);
        var include = CoverageWorkspace.ExpandNames(options.Include).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var repos = Directory.EnumerateDirectories(root, "novolis-*")
            .Select(path => new DirectoryInfo(path))
            .Where(dir => !exclude.Contains(dir.Name))
            .Where(dir => include.Count == 0 || include.Contains(dir.Name))
            .OrderBy(dir => dir.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var results = repos
            .AsParallel()
            .WithDegreeOfParallelism(options.ThrottleLimit > 0
                ? options.ThrottleLimit
                : Math.Max(1, Environment.ProcessorCount - 1))
            .Select(dir => ScanRepo(dir, options.PackableOnly, options.IncludeExecutables))
            .OrderBy(r => r.Repo, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new TestGapReport
        {
            Root = root,
            GeneratedUtc = DateTime.UtcNow,
            PackableOnly = options.PackableOnly,
            Repos = results,
        };
    }

    /// <summary>Markdown summary of the gap report.</summary>
    public static string FormatMarkdown(TestGapReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Novolis test gap report");
        sb.AppendLine();
        sb.AppendLine($"Generated: {report.GeneratedUtc:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine(
            $"Repos scanned: {report.Repos.Count}  |  Without tests: {report.ReposWithoutTestHosts.Count}  |  Untested assemblies: {report.UntestedAssemblies.Count}");
        sb.AppendLine();
        sb.AppendLine(
            "Metric is **direct** `ProjectReference` from a test host (`tests/**` + TUnit/xUnit/NUnit/VSTest). Transitive-only use does not count.");
        sb.AppendLine();
        sb.AppendLine("## Repos without test hosts");
        sb.AppendLine();
        if (report.ReposWithoutTestHosts.Count == 0)
        {
            sb.AppendLine("_None._");
        }
        else
        {
            sb.AppendLine("| Repo | Solutions | Production assemblies |");
            sb.AppendLine("|------|-----------|------------------------|");
            foreach (var repo in report.ReposWithoutTestHosts)
            {
                var sols = repo.Solutions.Count > 0 ? string.Join(", ", repo.Solutions) : "—";
                sb.AppendLine($"| {repo.Repo} | {sols} | {repo.ProductionCount} |");
            }
        }

        sb.AppendLine();
        sb.AppendLine("## Assemblies not referenced by any test host");
        sb.AppendLine();
        if (report.UntestedAssemblies.Count == 0)
        {
            sb.AppendLine("_None._");
        }
        else
        {
            sb.AppendLine("| Repo | PackageId | Path |");
            sb.AppendLine("|------|-----------|------|");
            foreach (var assembly in report.UntestedAssemblies.OrderBy(a => a.Repo, StringComparer.OrdinalIgnoreCase)
                         .ThenBy(a => a.PackageId, StringComparer.OrdinalIgnoreCase))
            {
                sb.AppendLine($"| {assembly.Repo} | {assembly.PackageId} | {assembly.RelPath} |");
            }
        }

        sb.AppendLine();
        sb.AppendLine("## Per-repo linkage");
        sb.AppendLine();
        sb.AppendLine("| Repo | Test hosts | Assemblies | Linked | Untested | Linked % |");
        sb.AppendLine("|------|------------|------------|--------|----------|----------|");
        foreach (var repo in report.Repos)
        {
            var pct = repo.CoveragePct is null
                ? "—"
                : repo.CoveragePct.Value.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture);
            sb.AppendLine(
                $"| {repo.Repo} | {repo.TestHostCount} | {repo.ProductionCount} | {repo.TestedCount} | {repo.UntestedCount} | {pct} |");
        }

        return sb.ToString();
    }

    /// <summary>Write SUMMARY.md and summary.json under <paramref name="outputDir"/>.</summary>
    public static (string MarkdownPath, string JsonPath) Write(TestGapReport report, string outputDir)
    {
        Directory.CreateDirectory(outputDir);
        var mdPath = Path.Combine(outputDir, "SUMMARY.md");
        var jsonPath = Path.Combine(outputDir, "summary.json");
        File.WriteAllText(mdPath, FormatMarkdown(report));
        var json = JsonSerializer.Serialize(new
        {
            report.GeneratedUtc,
            report.PackableOnly,
            ReposScanned = report.Repos.Count,
            ReposWithoutTestHosts = report.ReposWithoutTestHosts.Select(r => r.Repo).ToArray(),
            UntestedAssemblies = report.UntestedAssemblies,
            Repos = report.Repos.Select(r => new
            {
                r.Repo,
                r.TestHostCount,
                r.ProductionCount,
                r.TestedCount,
                r.UntestedCount,
                r.CoveragePct,
                r.NoTestHosts,
                r.Solutions,
            }),
        }, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(jsonPath, json);
        return (mdPath, jsonPath);
    }

    private static TestGapRepoResult ScanRepo(DirectoryInfo repo, bool packableOnly, bool includeExecutables)
    {
        var testProjects = Directory.Exists(Path.Combine(repo.FullName, "tests"))
            ? Directory.EnumerateFiles(Path.Combine(repo.FullName, "tests"), "*.csproj", SearchOption.AllDirectories)
                .Where(TestHostDiscovery.IsTestHostProject)
                .ToArray()
            : [];

        var slnxFiles = Directory.EnumerateFiles(repo.FullName, "*.slnx", SearchOption.TopDirectoryOnly)
            .Select(Path.GetFileName)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Cast<string>()
            .ToArray();
        var hasSrc = Directory.Exists(Path.Combine(repo.FullName, "src"))
            || Directory.Exists(Path.Combine(repo.FullName, "codegen"));

        var tested = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var testProject in testProjects)
        {
            var dir = Path.GetDirectoryName(testProject)!;
            foreach (Match match in ProjectReferenceInclude.Matches(File.ReadAllText(testProject)))
            {
                var raw = match.Groups[1].Value.Replace('/', Path.DirectorySeparatorChar);
                var resolved = Path.GetFullPath(Path.Combine(dir, raw));
                if (resolved.StartsWith(repo.FullName, StringComparison.OrdinalIgnoreCase))
                    tested.Add(resolved);
            }
        }

        var production = new List<TestGapAssembly>();
        foreach (var dirName in new[] { "src", "codegen" })
        {
            var dir = Path.Combine(repo.FullName, dirName);
            if (!Directory.Exists(dir))
                continue;
            foreach (var csproj in Directory.EnumerateFiles(dir, "*.csproj", SearchOption.AllDirectories))
            {
                if (SkipProductionPath.IsMatch(csproj))
                    continue;
                var text = File.ReadAllText(csproj);
                if (Regex.IsMatch(text, @"(?i)<IsTestProject>\s*true\s*</IsTestProject>"))
                    continue;
                if (Regex.IsMatch(text, @"(?i)<OutputType>\s*(WinExe|Exe)\s*</OutputType>") && !includeExecutables)
                    continue;
                var isPackable = !Regex.IsMatch(text, @"(?i)<IsPackable>\s*false\s*</IsPackable>");
                if (packableOnly && !isPackable)
                    continue;
                var packageId = Path.GetFileNameWithoutExtension(csproj);
                var idMatch = Regex.Match(text, @"(?i)<PackageId>\s*([^<]+)\s*</PackageId>");
                if (idMatch.Success)
                    packageId = idMatch.Groups[1].Value.Trim();
                production.Add(new TestGapAssembly
                {
                    Repo = repo.Name,
                    PackageId = packageId,
                    RelPath = Path.GetRelativePath(repo.FullName, csproj).Replace('\\', '/'),
                    Packable = isPackable,
                    Tested = tested.Contains(csproj),
                });
            }
        }

        var testedCount = production.Count(p => p.Tested);
        return new TestGapRepoResult
        {
            Repo = repo.Name,
            Path = repo.FullName,
            Solutions = slnxFiles,
            TestHostCount = testProjects.Length,
            ProductionCount = production.Count,
            TestedCount = testedCount,
            UntestedCount = production.Count - testedCount,
            NoTestHosts = testProjects.Length == 0 && (hasSrc || slnxFiles.Length > 0),
            Untested = production.Where(p => !p.Tested).OrderBy(p => p.PackageId, StringComparer.OrdinalIgnoreCase).ToArray(),
            CoveragePct = production.Count > 0 ? Math.Round(100.0 * testedCount / production.Count, 1) : null,
        };
    }
}
