using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Novolis.Tools.Docs.Org;

/// <summary>Options for regenerating org profile README status tables.</summary>
public sealed class OrgLandingStatusOptions
{
    /// <summary>GitHub organization login.</summary>
    public string Org { get; init; } = "Novolis-Platform";

    /// <summary>Path to profile/README.md.</summary>
    public required string ProfileReadmePath { get; init; }

    /// <summary>Max parallel gh/API calls.</summary>
    public int ThrottleLimit { get; init; } = 16;

    /// <summary>Max package IDs shown per repository row.</summary>
    public int MaxPackagesPerRepo { get; init; } = 3;
}

/// <summary>Replaces the novolis-org-status block in the org profile README.</summary>
public static partial class OrgLandingStatusUpdater
{
    /// <summary>Start marker for generated org status tables.</summary>
    public const string StartMarker = "<!-- novolis-org-status:start -->";

    /// <summary>End marker for generated org status tables.</summary>
    public const string EndMarker = "<!-- novolis-org-status:end -->";

    /// <summary>Queries GitHub and rewrites the status block in profile/README.md.</summary>
    public static void Run(OrgLandingStatusOptions options)
    {
        if (!File.Exists(options.ProfileReadmePath))
        {
            throw new FileNotFoundException("Profile README not found.", options.ProfileReadmePath);
        }

        Console.WriteLine($"Listing repositories for {options.Org}...");
        var repos = ListOrgRepos(options.Org);
        Console.WriteLine($"  {repos.Count} public non-archived repos");

        Console.WriteLine("Discovering workflows...");
        var repoWorkflows = DiscoverWorkflows(options.Org, repos);

        var repoJobs = repos
            .OrderBy(static r => r.Name, StringComparer.OrdinalIgnoreCase)
            .Select(r =>
            {
                repoWorkflows.TryGetValue(r.Name, out var files);
                files ??= [];
                return new RepoJob(
                    r.Name,
                    FindWorkflow(files, "pull-request.yml", "pull_request.yml", "ci.yml"),
                    FindWorkflow(files, "merge.yml"),
                    FindWorkflow(files, "release.yml"));
            })
            .ToArray();

        Console.WriteLine($"Resolving workflow conclusions (throttle={options.ThrottleLimit})...");
        var statusMap = new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        Parallel.ForEach(
            repoJobs,
            new ParallelOptions { MaxDegreeOfParallelism = options.ThrottleLimit },
            job =>
            {
                ResolveStatus(options.Org, job, statusMap);
            });

        Console.WriteLine("Listing NuGet packages on GitHub Packages...");
        var packages = ListNuGetPackages(options.Org);
        Console.WriteLine($"  {packages.Count} packages");

        Console.WriteLine($"Resolving latest versions (throttle={options.ThrottleLimit})...");
        var versionMap = new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var nugetOrgMap = new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        Parallel.ForEach(
            packages,
            new ParallelOptions { MaxDegreeOfParallelism = options.ThrottleLimit },
            pkg =>
            {
                ResolvePackageVersions(options.Org, pkg.Name, http, versionMap, nugetOrgMap);
            });

        var generatedAt = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm") + " UTC";
        var successMerges = repoJobs.Count(j =>
            statusMap.TryGetValue($"{j.Name}|merge", out var c) && c == "success");

        var packagesByRepo = GroupPackagesByRepo(packages);
        var block = BuildBlock(
            options,
            generatedAt,
            successMerges,
            repoJobs,
            packages,
            packagesByRepo,
            statusMap,
            versionMap,
            nugetOrgMap);

        var body = File.ReadAllText(options.ProfileReadmePath);
        if (!body.Contains(StartMarker, StringComparison.Ordinal) || !body.Contains(EndMarker, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Missing {StartMarker} / {EndMarker} in {options.ProfileReadmePath}.");
        }

        var pattern = "(?s)" + Regex.Escape(StartMarker) + ".*?" + Regex.Escape(EndMarker);
        var updated = Regex.Replace(body, pattern, block.TrimEnd());
        if (!updated.EndsWith('\n'))
        {
            updated += Environment.NewLine;
        }

        File.WriteAllText(options.ProfileReadmePath, updated, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        Console.WriteLine($"Updated {options.ProfileReadmePath}");
    }

    private sealed record RepoInfo(string Name, bool Archived);

    private sealed record RepoJob(string Name, string? Pr, string? Merge, string? Release);

    private sealed record PackageInfo(string Name, string? RepoName, string? HtmlUrl);

    private static List<RepoInfo> ListOrgRepos(string org)
    {
        var repos = new List<RepoInfo>();
        for (var page = 1; ; page++)
        {
            var json = GhProcess.RunGh(["api", $"orgs/{org}/repos?per_page=100&page={page}&type=public"]);
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array || doc.RootElement.GetArrayLength() == 0)
            {
                break;
            }

            foreach (var r in doc.RootElement.EnumerateArray())
            {
                if (r.TryGetProperty("archived", out var archived) && archived.GetBoolean())
                {
                    continue;
                }

                var name = r.GetProperty("name").GetString() ?? string.Empty;
                repos.Add(new RepoInfo(name, false));
            }

            if (doc.RootElement.GetArrayLength() < 100)
            {
                break;
            }
        }

        return repos;
    }

    private static Dictionary<string, List<string>> DiscoverWorkflows(string org, IReadOnlyList<RepoInfo> repos)
    {
        var map = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var repo in repos.OrderBy(static r => r.Name, StringComparer.OrdinalIgnoreCase))
        {
            var files = new List<string>();
            try
            {
                var json = GhProcess.RunGh(["api", $"repos/{org}/{repo.Name}/contents/.github/workflows"]);
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var c in doc.RootElement.EnumerateArray())
                    {
                        var name = c.GetProperty("name").GetString();
                        if (name is not null && WorkflowFileRegex().IsMatch(name))
                        {
                            files.Add(name);
                        }
                    }
                }
            }
            catch
            {
                // no workflows folder
            }

            map[repo.Name] = files;
        }

        return map;
    }

    [GeneratedRegex("\\.ya?ml$", RegexOptions.IgnoreCase)]
    private static partial Regex WorkflowFileRegex();

    private static string? FindWorkflow(IReadOnlyList<string> files, params string[] candidates)
    {
        foreach (var c in candidates)
        {
            if (files.Contains(c, StringComparer.OrdinalIgnoreCase))
            {
                return c;
            }
        }

        return null;
    }

    private static void ResolveStatus(string org, RepoJob job, ConcurrentDictionary<string, string> statusMap)
    {
        foreach (var pair in new (string Key, string? File, bool PreferMain)[]
                 {
                     ($"{job.Name}|pr", job.Pr, false),
                     ($"{job.Name}|merge", job.Merge, true),
                     ($"{job.Name}|release", job.Release, false),
                 })
        {
            if (string.IsNullOrEmpty(pair.File))
            {
                statusMap.TryAdd(pair.Key, string.Empty);
                continue;
            }

            var qs = pair.PreferMain
                ? "per_page=5&branch=main&status=completed"
                : "per_page=5&status=completed";
            var conclusion = string.Empty;
            try
            {
                var json = GhProcess.RunGh([
                    "api",
                    $"repos/{org}/{job.Name}/actions/workflows/{pair.File}/runs?{qs}",
                ], ignoreFailure: true);
                if (!string.IsNullOrWhiteSpace(json))
                {
                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("workflow_runs", out var runs) &&
                        runs.ValueKind == JsonValueKind.Array &&
                        runs.GetArrayLength() > 0)
                    {
                        conclusion = runs[0].GetProperty("conclusion").GetString() ?? string.Empty;
                    }
                }
            }
            catch
            {
                // leave empty
            }

            statusMap.TryAdd(pair.Key, conclusion);
        }
    }

    private static List<PackageInfo> ListNuGetPackages(string org)
    {
        var packages = new List<PackageInfo>();
        for (var page = 1; ; page++)
        {
            var json = GhProcess.RunGh(["api", $"orgs/{org}/packages?package_type=nuget&per_page=100&page={page}"]);
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array || doc.RootElement.GetArrayLength() == 0)
            {
                break;
            }

            foreach (var p in doc.RootElement.EnumerateArray())
            {
                var name = p.GetProperty("name").GetString() ?? string.Empty;
                string? repoName = null;
                if (p.TryGetProperty("repository", out var repo) && repo.ValueKind == JsonValueKind.Object &&
                    repo.TryGetProperty("name", out var rn))
                {
                    repoName = rn.GetString();
                }

                var htmlUrl = p.TryGetProperty("html_url", out var hu) ? hu.GetString() : null;
                packages.Add(new PackageInfo(name, repoName, htmlUrl));
            }

            if (doc.RootElement.GetArrayLength() < 100)
            {
                break;
            }
        }

        return packages;
    }

    private static void ResolvePackageVersions(
        string org,
        string pkgName,
        HttpClient http,
        ConcurrentDictionary<string, string> versionMap,
        ConcurrentDictionary<string, string> nugetOrgMap)
    {
        var latest = string.Empty;
        try
        {
            latest = GhProcess.RunGh([
                "api",
                $"orgs/{org}/packages/nuget/{pkgName}/versions?per_page=1",
                "-q",
                ".[0].name",
            ], ignoreFailure: true).Trim();
        }
        catch
        {
            // ignore
        }

        versionMap.TryAdd(pkgName, latest);

        var nugetLatest = string.Empty;
        try
        {
            var id = pkgName.ToLowerInvariant();
            var idx = http.GetFromJsonAsync<NuGetFlatIndex>($"https://api.nuget.org/v3-flatcontainer/{id}/index.json")
                .GetAwaiter().GetResult();
            if (idx?.Versions is { Count: > 0 })
            {
                nugetLatest = idx.Versions[^1];
            }
        }
        catch
        {
            // ignore
        }

        nugetOrgMap.TryAdd(pkgName, nugetLatest);
    }

    private sealed class NuGetFlatIndex
    {
        public List<string>? Versions { get; set; }
    }

    private static Dictionary<string, List<PackageInfo>> GroupPackagesByRepo(IReadOnlyList<PackageInfo> packages)
    {
        var map = new Dictionary<string, List<PackageInfo>>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in packages)
        {
            if (string.IsNullOrEmpty(p.RepoName))
            {
                continue;
            }

            if (!map.TryGetValue(p.RepoName, out var list))
            {
                list = [];
                map[p.RepoName] = list;
            }

            list.Add(p);
        }

        return map;
    }

    private static string BuildBlock(
        OrgLandingStatusOptions options,
        string generatedAt,
        int successMerges,
        IReadOnlyList<RepoJob> repoJobs,
        IReadOnlyList<PackageInfo> packages,
        IReadOnlyDictionary<string, List<PackageInfo>> packagesByRepo,
        ConcurrentDictionary<string, string> statusMap,
        ConcurrentDictionary<string, string> versionMap,
        ConcurrentDictionary<string, string> nugetOrgMap)
    {
        var orphanPackages = packages.Where(p => string.IsNullOrEmpty(p.RepoName)).ToList();
        var enriched = repoJobs.Select(j => EnrichRepo(options.Org, j, packagesByRepo, statusMap)).ToArray();
        var packageRepos = enriched.Where(r => r.HasPackages).OrderBy(static r => r.Name, StringComparer.OrdinalIgnoreCase).ToArray();
        var releaseRepos = enriched.Where(r => !r.HasPackages && r.HasReleaseWf).OrderBy(static r => r.Name, StringComparer.OrdinalIgnoreCase).ToArray();
        var otherRepos = enriched.Where(r => !r.HasPackages && !r.HasReleaseWf).OrderBy(static r => r.Name, StringComparer.OrdinalIgnoreCase).ToArray();

        var sb = new StringBuilder();
        sb.AppendLine(StartMarker);
        sb.AppendLine();
        sb.AppendLine($"<!-- Generated by novolis-docs org-readme — do not hand-edit. Last run: {generatedAt} -->");
        sb.AppendLine();
        sb.AppendLine($"CI badges are **live** GitHub Actions SVGs. Package versions and the merge-success count below are snapshotted at regen ({generatedAt}).");
        sb.AppendLine();
        sb.AppendLine(
            $"Merge successes (at last regen): **{successMerges}** / {repoJobs.Count}. NuGet packages on GPR: **{packages.Count}** · [org packages](https://github.com/orgs/{options.Org}/packages) · [novolis-registry](https://github.com/{options.Org}/novolis-registry).");
        sb.AppendLine();

        sb.AppendLine("### Packages");
        sb.AppendLine();
        sb.AppendLine($"Repos that publish to GitHub Packages (top **{options.MaxPackagesPerRepo}** package IDs + count).");
        sb.AppendLine();
        sb.AppendLine("| Repository | PR | Merge | Packages |");
        sb.AppendLine("|------------|----|-------|----------|");
        foreach (var row in packageRepos)
        {
            packagesByRepo.TryGetValue(row.Name, out var pkgs);
            var pkgCell = FormatPackageCell(options.Org, options.MaxPackagesPerRepo, row.Name, pkgs ?? [], versionMap, nugetOrgMap);
            sb.AppendLine($"| {row.Link} | {row.Pr} | {row.Merge} | {pkgCell} |");
        }

        if (orphanPackages.Count > 0)
        {
            var pkgCell = FormatPackageCell(options.Org, options.MaxPackagesPerRepo, string.Empty, orphanPackages, versionMap, nugetOrgMap);
            sb.AppendLine($"| *(unlinked packages)* | — | — | {pkgCell} |");
        }

        if (packageRepos.Length == 0 && orphanPackages.Count == 0)
        {
            sb.AppendLine("| — | — | — | — |");
        }

        sb.AppendLine();
        sb.AppendLine("### Other");
        sb.AppendLine();
        sb.AppendLine("Repos without NuGet packages and without a `release.yml` workflow (infra, templates, labs, etc.).");
        sb.AppendLine();
        sb.AppendLine("| Repository | PR | Merge |");
        sb.AppendLine("|------------|----|-------|");
        if (otherRepos.Length == 0)
        {
            sb.AppendLine("| — | — | — |");
        }
        else
        {
            foreach (var row in otherRepos)
            {
                sb.AppendLine($"| {row.Link} | {row.Pr} | {row.Merge} |");
            }
        }

        sb.AppendLine();
        sb.AppendLine("### Releases");
        sb.AppendLine();
        sb.AppendLine("Repos with `release.yml` that do **not** publish NuGet packages (apps / installers).");
        sb.AppendLine();
        sb.AppendLine("| Repository | Release |");
        sb.AppendLine("|------------|---------|");
        if (releaseRepos.Length == 0)
        {
            sb.AppendLine("| — | — |");
        }
        else
        {
            foreach (var row in releaseRepos)
            {
                sb.AppendLine($"| {row.Link} | {row.Release} |");
            }
        }

        sb.AppendLine();
        sb.AppendLine(EndMarker);
        return sb.ToString();
    }

    private sealed record EnrichedRepo(
        string Name,
        string Link,
        string Pr,
        string Merge,
        string Release,
        bool HasPackages,
        bool HasReleaseWf);

    private static EnrichedRepo EnrichRepo(
        string org,
        RepoJob job,
        IReadOnlyDictionary<string, List<PackageInfo>> packagesByRepo,
        ConcurrentDictionary<string, string> statusMap)
    {
        statusMap.TryGetValue($"{job.Name}|pr", out var prConc);
        statusMap.TryGetValue($"{job.Name}|merge", out var mergeConc);
        statusMap.TryGetValue($"{job.Name}|release", out var releaseConc);

        var prBadge = job.Pr is not null && prConc == "success"
            ? StatusShield(org, job.Name, job.Pr, "PR")
            : "—";
        var mergeBadge = job.Merge is not null && !string.IsNullOrEmpty(mergeConc)
            ? StatusShield(org, job.Name, job.Merge, "merge")
            : "—";
        var releaseBadge = job.Release is not null && !string.IsNullOrEmpty(releaseConc)
            ? StatusShield(org, job.Name, job.Release, "release")
            : "—";

        var hasPackages = packagesByRepo.ContainsKey(job.Name) && packagesByRepo[job.Name].Count > 0;
        return new EnrichedRepo(
            job.Name,
            $"[`{job.Name}`](https://github.com/{org}/{job.Name})",
            prBadge,
            mergeBadge,
            releaseBadge,
            hasPackages,
            job.Release is not null);
    }

    private static string StatusShield(string org, string repo, string workflowFile, string label)
    {
        var href = $"https://github.com/{org}/{repo}/actions/workflows/{workflowFile}";
        var badgeQuery = workflowFile == "merge.yml" ? "?branch=main" : string.Empty;
        var badge = $"https://github.com/{org}/{repo}/actions/workflows/{workflowFile}/badge.svg{badgeQuery}";
        return $"[![{label}]({badge})]({href})";
    }

    private static string FormatPackageCell(
        string org,
        int maxPackages,
        string repoName,
        IReadOnlyList<PackageInfo> pkgs,
        ConcurrentDictionary<string, string> versionMap,
        ConcurrentDictionary<string, string> nugetOrgMap)
    {
        if (pkgs.Count == 0)
        {
            return "—";
        }

        var expected = ExpectedAggregateId(repoName);
        var sorted = pkgs
            .OrderBy(p => expected is not null && p.Name == expected ? 0 : 1)
            .ThenBy(p => p.Name.Split('.').Length)
            .ThenBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var shown = sorted.Take(maxPackages).ToArray();
        var lines = new List<string>();
        foreach (var p in shown)
        {
            versionMap.TryGetValue(p.Name, out var gprVer);
            var pkgUrl = p.HtmlUrl ?? $"https://github.com/orgs/{org}/packages/nuget/package/{p.Name}";
            var shield = VersionShield(gprVer ?? string.Empty, pkgUrl, "GPR");
            nugetOrgMap.TryGetValue(p.Name, out var nugetVer);
            var nugetPart = !string.IsNullOrWhiteSpace(nugetVer)
                ? " " + VersionShield(nugetVer, $"https://www.nuget.org/packages/{p.Name}", "nuget.org")
                : string.Empty;
            lines.Add($"``{p.Name}`` {shield}{nugetPart}");
        }

        var more = sorted.Length - shown.Length;
        if (more > 0)
        {
            lines.Add($"_+{more} more_ → [packages](https://github.com/orgs/{org}/packages?repo_name={repoName})");
        }

        return string.Join("<br>", lines);
    }

    private static string? ExpectedAggregateId(string repoName)
    {
        if (!repoName.StartsWith("novolis-", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var tail = repoName["novolis-".Length..];
        var parts = tail.Split('-', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            return null;
        }

        var titled = string.Join('.', parts.Select(static p =>
            p.Length == 0 ? p : char.ToUpperInvariant(p[0]) + p[1..]));
        return "Novolis." + titled;
    }

    private static string VersionShield(string version, string href, string label)
    {
        if (string.IsNullOrWhiteSpace(version))
        {
            return "—";
        }

        var url = $"https://img.shields.io/badge/{ShieldPath(label)}-{ShieldPath(version)}-brightgreen";
        return $"[![{label} {version}]({url})]({href})";
    }

    private static string ShieldPath(string text) =>
        text.Replace("-", "--", StringComparison.Ordinal)
            .Replace("_", "__", StringComparison.Ordinal)
            .Replace(" ", "_", StringComparison.Ordinal);
}
