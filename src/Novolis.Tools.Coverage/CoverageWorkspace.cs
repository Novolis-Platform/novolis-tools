using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Novolis.Tools.Coverage;

/// <summary>Workspace root + exclude helpers.</summary>
public static class CoverageWorkspace
{
    /// <summary>Resolve org root from <c>NOVOLIS_ROOT</c>, cwd walk, or explicit path.</summary>
    public static string ResolveRoot(string? explicitRoot = null)
    {
        if (!string.IsNullOrWhiteSpace(explicitRoot))
            return Path.GetFullPath(explicitRoot);

        var env = Environment.GetEnvironmentVariable("NOVOLIS_ROOT");
        if (!string.IsNullOrWhiteSpace(env) && Directory.Exists(env))
            return Path.GetFullPath(env);

        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir is not null)
        {
            var marker = Path.Combine(dir.FullName, "Novolis.Platform.slnx");
            var gov = Path.Combine(dir.FullName, "novolis-governance");
            if (File.Exists(marker) || Directory.Exists(gov))
                return dir.FullName;
            dir = dir.Parent;
        }

        throw new InvalidOperationException(
            "Could not resolve Novolis workspace root. Pass --root or set NOVOLIS_ROOT.");
    }

    /// <summary>Default exclude file under governance scripts.</summary>
    public static string DefaultExcludeFile(string root) =>
        Path.Combine(root, "novolis-governance", "scripts", "coverage-excludes.txt");

    /// <summary>Split comma lists and trim.</summary>
    public static IReadOnlyList<string> ExpandNames(IEnumerable<string>? names)
    {
        var list = new List<string>();
        if (names is null)
            return list;
        foreach (var n in names)
        {
            if (string.IsNullOrWhiteSpace(n))
                continue;
            foreach (var part in n.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (part.Length > 0)
                    list.Add(part);
            }
        }

        return list;
    }

    /// <summary>Merge exclude file + CLI excludes.</summary>
    public static IReadOnlySet<string> ReadExcludes(string? excludeFile, IEnumerable<string>? extra)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var e in ExpandNames(extra))
            set.Add(e);

        if (!string.IsNullOrWhiteSpace(excludeFile) && File.Exists(excludeFile))
        {
            foreach (var raw in File.ReadLines(excludeFile))
            {
                var line = raw.Trim();
                if (line.Length == 0 || line.StartsWith('#'))
                    continue;
                set.Add(line);
            }
        }

        return set;
    }

    /// <summary>Locate Platform.slnx under the workspace.</summary>
    public static string ResolvePlatformSlnx(string root, string? explicitPath = null)
    {
        if (!string.IsNullOrWhiteSpace(explicitPath))
        {
            var p = Path.GetFullPath(explicitPath);
            if (!File.Exists(p))
                throw new FileNotFoundException("Platform.slnx not found.", p);
            return p;
        }

        var rootCopy = Path.Combine(root, "Novolis.Platform.slnx");
        if (File.Exists(rootCopy))
            return Path.GetFullPath(rootCopy);

        var buildCopy = Path.Combine(root, "novolis-governance", "build", "Novolis.Platform.slnx");
        if (File.Exists(buildCopy))
            return Path.GetFullPath(buildCopy);

        throw new FileNotFoundException(
            $"Novolis.Platform.slnx not found under {root}.");
    }
}

/// <summary>Discover MTP test hosts.</summary>
public static class TestHostDiscovery
{
    private static readonly Regex IsTestProjectFalse = new(
        @"<IsTestProject>\s*false\s*</IsTestProject>",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex IsTestingPlatformAppFalse = new(
        @"<IsTestingPlatformApplication>\s*false\s*</IsTestingPlatformApplication>",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex TestFrameworkRef = new(
        @"TUnit|Microsoft\.NET\.Test\.Sdk|xunit|NUnit",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex ProjectPathAttr = new(
        @"Project\s+Path=""([^""]+\.csproj)""",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    /// <summary>True when the csproj is an MTP / TUnit host under tests/.</summary>
    public static bool IsTestHostProject(string projectPath)
    {
        if (!projectPath.Contains($"{Path.DirectorySeparatorChar}tests{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
            && !projectPath.Contains("/tests/", StringComparison.OrdinalIgnoreCase)
            && !projectPath.Contains("\\tests\\", StringComparison.OrdinalIgnoreCase))
            return false;

        var text = File.ReadAllText(projectPath);
        if (IsTestProjectFalse.IsMatch(text))
            return false;
        if (IsTestingPlatformAppFalse.IsMatch(text))
            return false;
        return TestFrameworkRef.IsMatch(text);
    }

    /// <summary>Per-repo discovery under <c>novolis-*</c>.</summary>
    public static IReadOnlyList<CoverageRepo> DiscoverRepos(
        string root,
        IReadOnlySet<string> exclude,
        IReadOnlySet<string>? include)
    {
        var repos = new List<CoverageRepo>();
        foreach (var dir in Directory.EnumerateDirectories(root, "novolis-*").OrderBy(d => d, StringComparer.OrdinalIgnoreCase))
        {
            var name = Path.GetFileName(dir);
            if (exclude.Contains(name))
                continue;
            if (include is { Count: > 0 } && !include.Contains(name))
                continue;

            var tests = DiscoverTestProjects(dir);
            if (tests.Count == 0)
                continue;

            var slnx = Directory.EnumerateFiles(dir, "*.slnx", SearchOption.TopDirectoryOnly).FirstOrDefault();
            repos.Add(new CoverageRepo
            {
                Name = name,
                Path = dir,
                Solution = slnx,
                TestProjects = tests,
            });
        }

        return repos;
    }

    /// <summary>Hosts listed in Platform.slnx (tests/ only).</summary>
    public static IReadOnlyList<CoverageRepo> DiscoverFromPlatformSlnx(
        string root,
        string slnxPath,
        IReadOnlySet<string> exclude,
        IReadOnlySet<string>? include)
    {
        var slnxDir = Path.GetDirectoryName(Path.GetFullPath(slnxPath))
                      ?? throw new InvalidOperationException(slnxPath);
        var rootFull = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var byRepo = new Dictionary<string, (string Path, List<string> Projects)>(StringComparer.OrdinalIgnoreCase);
        var text = File.ReadAllText(slnxPath);

        foreach (Match m in ProjectPathAttr.Matches(text))
        {
            var rel = m.Groups[1].Value
                .Replace('/', Path.DirectorySeparatorChar)
                .Replace('\\', Path.DirectorySeparatorChar);
            if (!rel.Contains($"{Path.DirectorySeparatorChar}tests{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
                && !rel.Contains("/tests/", StringComparison.OrdinalIgnoreCase)
                && !rel.Contains("\\tests\\", StringComparison.OrdinalIgnoreCase))
                continue;

            var full = Path.GetFullPath(Path.Combine(slnxDir, rel));
            if (!File.Exists(full))
                continue;
            if (!full.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase))
                continue;

            var relFromRoot = full[rootFull.Length..].TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var repoName = relFromRoot.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)[0];
            if (!repoName.StartsWith("novolis-", StringComparison.OrdinalIgnoreCase))
                continue;
            if (exclude.Contains(repoName))
                continue;
            if (include is { Count: > 0 } && !include.Contains(repoName))
                continue;
            if (!IsTestHostProject(full))
                continue;

            if (!byRepo.TryGetValue(repoName, out var entry))
            {
                entry = (Path.Combine(root, repoName), []);
                byRepo[repoName] = entry;
            }

            if (!entry.Projects.Contains(full, StringComparer.OrdinalIgnoreCase))
                entry.Projects.Add(full);
        }

        return byRepo.Keys
            .OrderBy(k => k, StringComparer.OrdinalIgnoreCase)
            .Select(k =>
            {
                var e = byRepo[k];
                return new CoverageRepo
                {
                    Name = k,
                    Path = e.Path,
                    Solution = slnxPath,
                    TestProjects = e.Projects,
                };
            })
            .ToList();
    }

    private static IReadOnlyList<string> DiscoverTestProjects(string repoPath)
    {
        var list = new List<string>();
        var testsRoot = Path.Combine(repoPath, "tests");
        if (!Directory.Exists(testsRoot))
            return list;

        foreach (var proj in Directory.EnumerateFiles(testsRoot, "*.csproj", SearchOption.AllDirectories))
        {
            if (IsTestHostProject(proj))
                list.Add(proj);
        }

        return list;
    }
}

/// <summary>Parse Cobertura XML summaries.</summary>
public static class CoberturaSummaryParser
{
    /// <summary>Read rates from a Cobertura file.</summary>
    public static CoberturaSummary Parse(string coberturaPath)
    {
        var doc = XDocument.Load(coberturaPath);
        var coverage = doc.Root ?? throw new InvalidOperationException($"No root element in {coberturaPath}");
        static double AttrDouble(XElement el, string name) =>
            double.TryParse((string?)el.Attribute(name), System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var v)
                ? v
                : 0;
        static int AttrInt(XElement el, string name) =>
            int.TryParse((string?)el.Attribute(name), System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture, out var v)
                ? v
                : 0;

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
}
