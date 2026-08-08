using System.Text.RegularExpressions;

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

    /// <summary>
    /// ReportGenerator assembly include filter for a repo so ProjectRef transitive
    /// siblings do not drag per-repo SUMMARY percentages.
    /// </summary>
    public static string RepoAssemblyFilter(string repoName)
    {
        var special = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["novolis-gaming"] = "+Novolis.Game*",
            ["novolis-xsd"] = "+Novolis.Xsd*",
            ["novolis-analyzers"] = "+Novolis.Analyzers.*;-Novolis.Analyzers.Licensing",
            ["novolis-tools"] = "+Novolis.Tools*",
            ["novolis-logging"] = "+Novolis.Logging*",
            ["novolis-civics"] = "+Novolis.Civics*",
            ["novolis-simulation"] = "+Novolis.Simulation*",
            ["novolis-economy"] = "+Novolis.Economy*",
            ["novolis-codegen"] = "+Novolis.CodeGen*",
            ["novolis-agent"] = "+Novolis.Agent*",
            ["novolis-storage"] = "+Novolis.Storage*",
            ["novolis-math"] = "+Novolis.Math*",
            ["novolis-physics"] = "+Novolis.Physics*",
            ["novolis-io"] = "+Novolis.IO*",
            ["novolis-cad"] = "+Novolis.Cad*",
            ["novolis-markup"] = "+Novolis.Markup*",
            ["novolis-video"] = "+Novolis.Video*",
            ["novolis-audio"] = "+Novolis.Audio*",
            ["novolis-rendering"] = "+Novolis.Rendering*",
            ["novolis-raylib"] = "+Novolis.Raylib*",
            ["novolis-avalonia"] = "+Novolis.Avalonia*",
            ["novolis-astro"] = "+Novolis.Astro*",
            ["novolis-geopolitics"] = "+Novolis.Geopolitics*",
            ["novolis-transports"] = "+Novolis.Transports*",
            ["novolis-testing"] = "+Novolis.Testing*",
            ["novolis-machinelearning"] = "+Novolis.MachineLearning*",
            ["novolis-manuscript"] = "+Novolis.Manuscript*",
            ["novolis-documents"] = "+Novolis.Documents*",
            ["novolis-workspaces"] = "+Novolis.Workspaces*;+Novolis.Snapshots*;+Novolis.Timeline*",
            ["novolis-msbuild"] = "+Novolis.MSBuild*",
        };

        if (special.TryGetValue(repoName, out var filter))
            return filter;

        if (repoName.StartsWith("novolis-", StringComparison.OrdinalIgnoreCase))
        {
            var parts = repoName["novolis-".Length..]
                .Split('-', StringSplitOptions.RemoveEmptyEntries)
                .Select(p => p.Length == 0 ? p : char.ToUpperInvariant(p[0]) + p[1..]);
            var dotted = "Novolis." + string.Join('.', parts);
            return $"+{dotted}*";
        }

        return "-Novolis.Analyzers.Licensing";
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
