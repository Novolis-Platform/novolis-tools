using System.Text.RegularExpressions;

namespace Novolis.Tools.Coverage;

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

    /// <summary>Hosts listed in Platform.slnx (tests/ only), plus on-disk tests for slnx repos whose tests were omitted from the meta solution.</summary>
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
        var reposInSlnx = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var text = File.ReadAllText(slnxPath);

        foreach (Match m in ProjectPathAttr.Matches(text))
        {
            var rel = m.Groups[1].Value
                .Replace('/', Path.DirectorySeparatorChar)
                .Replace('\\', Path.DirectorySeparatorChar);

            var full = Path.GetFullPath(Path.Combine(slnxDir, rel));
            if (!full.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase))
                continue;

            var relFromRoot = full[rootFull.Length..].TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var segments = relFromRoot.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (segments.Length == 0)
                continue;
            var repoName = segments[0];
            if (!repoName.StartsWith("novolis-", StringComparison.OrdinalIgnoreCase))
                continue;
            if (exclude.Contains(repoName))
                continue;
            if (include is { Count: > 0 } && !include.Contains(repoName))
                continue;

            reposInSlnx.Add(repoName);

            var isTestsPath = rel.Contains($"{Path.DirectorySeparatorChar}tests{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
                              || rel.Contains("/tests/", StringComparison.OrdinalIgnoreCase)
                              || rel.Contains("\\tests\\", StringComparison.OrdinalIgnoreCase);
            if (!isTestsPath)
                continue;
            if (!File.Exists(full))
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

        // Meta slnx intentionally omits some test hosts (e.g. Agent.Unit hang under full-platform
        // parallel). Still cover those libraries when tests exist on disk.
        foreach (var repoName in reposInSlnx)
        {
            if (byRepo.ContainsKey(repoName))
                continue;

            var repoPath = Path.Combine(root, repoName);
            var diskTests = DiscoverTestProjects(repoPath);
            if (diskTests.Count == 0)
                continue;

            byRepo[repoName] = (repoPath, diskTests.ToList());
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
