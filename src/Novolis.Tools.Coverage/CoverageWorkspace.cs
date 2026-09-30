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
