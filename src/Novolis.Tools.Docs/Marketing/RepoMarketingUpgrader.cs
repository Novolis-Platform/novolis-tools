using System.Diagnostics;
using System.Text.RegularExpressions;
using Novolis.Tools.Docs.Org;
using Novolis.Tools.Docs.Site;

namespace Novolis.Tools.Docs.Marketing;

/// <summary>Upgrades repo README marketing, banners, catalog, and optional GitHub metadata.</summary>
public static class RepoMarketingUpgrader
{
    private const string Org = "Novolis-Platform";

    private static readonly HashSet<string> SkipPkgReadmeRepos = new(StringComparer.OrdinalIgnoreCase)
    {
        "novolis-apps", "novolis-utilities", "novolis-lab", "novolis-experimental", "novolis-template-dotnet",
        ".github", "novolis-governance", "novolis-workflows", "novolis-registry",
    };

    private static readonly HashSet<string> SkipPackageIndexRepos = new(StringComparer.OrdinalIgnoreCase)
    {
        "novolis-apps", "novolis-utilities", "novolis-lab", "novolis-experimental", "novolis-template-dotnet",
        ".github", "novolis-governance", "novolis-workflows", "novolis-registry", "novolis-mapping",
        "novolis-scheduling", "novolis-wirefish",
    };

    private static readonly HashSet<string> LocalOnlyGhRepos = new(StringComparer.OrdinalIgnoreCase)
    {
        "novolis-mapping", "novolis-scheduling", "novolis-wirefish",
    };

    /// <summary>Runs the marketing upgrade across the workspace.</summary>
    public static RepoMarketingResult Run(RepoMarketingOptions options)
    {
        var catalog = new Dictionary<string, DocsRepoMeta>(PlatformRepoCatalog.Entries, StringComparer.OrdinalIgnoreCase);
        var result = new RepoMarketingResult();
        var bannerDir = Path.Combine(options.GitHubBrandRoot, "brand", "banners");
        Directory.CreateDirectory(bannerDir);

        if (!options.SkipBanners)
        {
            var profile = GraphicalProfileTokens.Load(options.ProfilePath);
            foreach (var (key, meta) in catalog)
            {
                var name = DocsRepoCatalog.BannerStem(key);
                MarketingBannerSvg.Write(key, meta, profile, Path.Combine(bannerDir, $"{name}.svg"));
            }

            Console.WriteLine($"Wrote banners to {bannerDir}");
        }

        var catalogJson = Path.Combine(options.GitHubBrandRoot, "site", "repo-catalog.json");
        RepoCatalogExporter.Export(catalog, catalogJson);
        Console.WriteLine($"Wrote docs catalog {catalogJson}");

        if (options.SkipReadmes)
        {
            Console.WriteLine("SkipReadmes: banners and catalog only.");
            return result;
        }

        var repoDirs = Directory.GetDirectories(options.WorkspaceRoot)
            .Select(d => new DirectoryInfo(d))
            .Where(d => d.Name.StartsWith("novolis-", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(d.Name, ".github", StringComparison.OrdinalIgnoreCase))
            .OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        foreach (var dir in repoDirs)
        {
            var name = dir.Name;
            if (!catalog.ContainsKey(name))
            {
                Console.WriteLine($"Warning: No catalog entry for {name} — using defaults");
                catalog[name] = PlatformRepoCatalog.GetOrDefault(name);
            }

            var meta = catalog[name];
            Console.WriteLine($"==> {name}");

            if (!SkipPkgReadmeRepos.Contains(name))
            {
                foreach (var sub in new[] { "src", "codegen" })
                {
                    var root = Path.Combine(dir.FullName, sub);
                    if (!Directory.Exists(root))
                    {
                        continue;
                    }

                    foreach (var csproj in Directory.EnumerateFiles(root, "*.csproj", SearchOption.AllDirectories))
                    {
                        if (PackageReadmeEnsurer.Ensure(csproj, name))
                        {
                            result.PackageReadmes++;
                        }
                    }
                }
            }

            if (!SkipPackageIndexRepos.Contains(name))
            {
                if (SyncPackageIndex(options.PackageIndexScriptPath, dir.FullName))
                {
                    result.Indexes++;
                }
            }
            else
            {
                StripPackageIndex(dir.FullName, name);
            }

            UpdateRepoReadme(dir.FullName, name, meta);
            result.Repos++;

            if (options.ApplyGitHubMeta && !LocalOnlyGhRepos.Contains(name))
            {
                if (ApplyGhMeta(name, meta))
                {
                    result.Meta++;
                }
            }
        }

        Console.WriteLine();
        Console.WriteLine(
            $"Done. Repos={result.Repos} packageReadmeTouches={result.PackageReadmes} indexes={result.Indexes} meta={result.Meta}");
        Console.WriteLine($"Banners: {bannerDir}");
        Console.WriteLine("Remember: commit+push .github first so banner URLs resolve, then other repos.");
        return result;
    }

    private static void UpdateRepoReadme(string repoRoot, string repoName, DocsRepoMeta meta)
    {
        var readmePath = Path.Combine(repoRoot, "README.md");
        var header = MarketingHeaderBuilder.Build(repoName, meta);

        if (!File.Exists(readmePath) || new FileInfo(readmePath).Length < 20)
        {
            var seed = $"""
                {header}# {repoName}

                {meta.Blurb}

                ## Get started

                Configure GitHub Packages once (from a sibling `novolis-governance` checkout):

                ```powershell
                pwsh -File ../novolis-governance/scripts/configure-gpr-user-nuget.ps1
                ```

                See [Novolis-Platform](https://github.com/{Org}) for the full ecosystem.
                """;
            WriteUtf8NoBom(readmePath, seed);
            return;
        }

        var body = File.ReadAllText(readmePath);
        body = MarketingHeaderBuilder.MergeIntoReadme(body, header);
        WriteUtf8NoBom(readmePath, body.TrimEnd() + Environment.NewLine);
    }

    private static void StripPackageIndex(string repoRoot, string name)
    {
        var readmePath = Path.Combine(repoRoot, "README.md");
        if (!File.Exists(readmePath))
        {
            return;
        }

        var body = File.ReadAllText(readmePath);
        var pattern = new Regex("(?s)<!-- novolis-package-index:start -->.*?<!-- novolis-package-index:end -->\\s*");
        if (!pattern.IsMatch(body))
        {
            return;
        }

        body = pattern.Replace(body, string.Empty);
        WriteUtf8NoBom(readmePath, body.TrimEnd() + Environment.NewLine);
        Console.WriteLine($"  stripped package index from {name}");
    }

    private static bool SyncPackageIndex(string scriptPath, string repoRoot)
    {
        if (!File.Exists(scriptPath))
        {
            Console.WriteLine($"Warning: Package index skip — missing script {scriptPath}");
            return false;
        }

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "pwsh",
                WorkingDirectory = Path.GetDirectoryName(scriptPath)!,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            psi.ArgumentList.Add("-NoProfile");
            psi.ArgumentList.Add("-File");
            psi.ArgumentList.Add(scriptPath);
            psi.ArgumentList.Add("-RepoRoot");
            psi.ArgumentList.Add(repoRoot);
            psi.ArgumentList.Add("-Replace");

            using var process = Process.Start(psi);
            if (process is null)
            {
                return false;
            }

            process.WaitForExit();
            return process.ExitCode == 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: Package index skip for {Path.GetFileName(repoRoot)}: {ex.Message}");
            return false;
        }
    }

    private static bool ApplyGhMeta(string repoName, DocsRepoMeta meta)
    {
        try
        {
            var ghName = string.Equals(repoName, ".github", StringComparison.OrdinalIgnoreCase) ? ".github" : repoName;
            GhProcess.Run("gh", ["repo", "edit", $"{Org}/{ghName}", "--description", meta.Desc]);
            foreach (var topic in meta.Topics)
            {
                GhProcess.Run("gh", ["repo", "edit", $"{Org}/{ghName}", "--add-topic", topic], ignoreFailure: true);
            }

            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: gh meta failed for {repoName}: {ex.Message}");
            return false;
        }
    }

    private static void WriteUtf8NoBom(string path, string content) =>
        File.WriteAllText(path, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
}
