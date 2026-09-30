using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Novolis.Tools.Docs.Org;

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

        Console.WriteLine($"Resolving releases and workflow runs (throttle={options.ThrottleLimit})...");
        var signals = new ConcurrentDictionary<string, OrgRepoSignals>(StringComparer.OrdinalIgnoreCase);
        Parallel.ForEach(
            repoJobs,
            new ParallelOptions { MaxDegreeOfParallelism = options.ThrottleLimit },
            job =>
            {
                signals[job.Name] = ReadSignals(options.Org, job);
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
        var packagesByRepo = GroupPackagesByRepo(packages);
        var facts = repoJobs
            .Select(job => ToFacts(
                job,
                signals.TryGetValue(job.Name, out var signal)
                    ? signal
                    : new OrgRepoSignals(null, null, null),
                packagesByRepo,
                versionMap,
                nugetOrgMap))
            .ToArray();
        var snapshot = OrgStatusSnapshotFactory.Create(options.Org, generatedAt, packages.Count, facts);
        var block = OrgStatusMarkdown.Build(snapshot);
        if (!string.IsNullOrWhiteSpace(options.StatusJsonPath))
        {
            var statusPath = Path.GetFullPath(options.StatusJsonPath);
            Directory.CreateDirectory(Path.GetDirectoryName(statusPath)!);
            File.WriteAllText(statusPath, JsonSerializer.Serialize(snapshot, OrgStatusJson.Options), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            Console.WriteLine($"Wrote {statusPath}");
        }

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
}
