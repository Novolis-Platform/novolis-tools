using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;

namespace Novolis.Tools.Docs.Org;

public static partial class OrgLandingStatusUpdater
{
    private static OrgRepoSignals ReadSignals(string org, RepoJob job)
    {
        try
        {
            return new(
                job.Merge is null ? null : ReadLatestRun(org, job.Name, job.Merge, preferMain: true),
                job.Release is null ? null : ReadLatestRun(org, job.Name, job.Release, preferMain: false),
                ReadLatestRelease(org, job.Name));
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException)
        {
            Console.WriteLine($"  {job.Name}: {ex.Message.Split('\n')[0]}");
            return new(null, null, null);
        }
    }

    private static OrgRepoFacts ToFacts(
        RepoJob job,
        OrgRepoSignals signals,
        IReadOnlyDictionary<string, List<PackageInfo>> packagesByRepo,
        ConcurrentDictionary<string, string> versionMap,
        ConcurrentDictionary<string, string> nugetOrgMap)
    {
        packagesByRepo.TryGetValue(job.Name, out var packages);
        packages ??= [];
        var gpr = OrgVersions.Latest(packages.Select(p => versionMap.TryGetValue(p.Name, out var v) ? v : null));
        var nuget = OrgVersions.Latest(packages.Select(p => nugetOrgMap.TryGetValue(p.Name, out var v) ? v : null));
        return new OrgRepoFacts(job.Name, packages.Count, gpr, nuget, signals.Merge, signals.ReleaseRun, signals.LatestRelease);
    }

    private static OrgWorkflowFact? ReadLatestRun(string org, string repo, string workflowFile, bool preferMain)
    {
        var qs = preferMain
            ? "per_page=8&branch=main&status=completed"
            : "per_page=8&status=completed";
        var json = GhProcess.RunGh(
            ["api", $"repos/{org}/{repo}/actions/workflows/{workflowFile}/runs?{qs}"],
            ignoreFailure: true);
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("workflow_runs", out var runs) ||
            runs.ValueKind != JsonValueKind.Array ||
            runs.GetArrayLength() == 0)
        {
            return null;
        }

        foreach (var run in runs.EnumerateArray())
        {
            var conclusion = StringProp(run, "conclusion");
            var runId = run.TryGetProperty("id", out var idEl) && idEl.TryGetInt64(out var id) ? id : 0L;
            string? error = null;
            if (runId > 0 && conclusion is "failure" or "cancelled")
            {
                error = FirstFailureAnnotation(org, repo, runId);
            }

            if (OrgFailureText.IsSupersededCancellation(error))
            {
                continue;
            }

            return new OrgWorkflowFact(
                workflowFile,
                conclusion,
                StringProp(run, "display_title"),
                StringProp(run, "html_url"),
                StringProp(run, "created_at"),
                error);
        }

        return null;
    }

    private static OrgReleaseFact? ReadLatestRelease(string org, string repo)
    {
        var json = GhProcess.RunGh(
            ["api", $"repos/{org}/{repo}/releases?per_page=5"],
            ignoreFailure: true);
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var release in doc.RootElement.EnumerateArray())
        {
            if (BoolProp(release, "draft"))
            {
                continue;
            }

            var published = StringProp(release, "published_at");
            if (string.IsNullOrWhiteSpace(published))
            {
                continue;
            }

            var assets = ReadAssets(release);
            return new OrgReleaseFact(
                StringProp(release, "tag_name"),
                published,
                StringProp(release, "html_url"),
                assets.Count,
                assets);
        }

        return null;
    }

    private static List<OrgReleaseAsset> ReadAssets(JsonElement release)
    {
        var assets = new List<OrgReleaseAsset>();
        if (!release.TryGetProperty("assets", out var assetEl) || assetEl.ValueKind != JsonValueKind.Array)
        {
            return assets;
        }

        foreach (var asset in assetEl.EnumerateArray())
        {
            var name = StringProp(asset, "name");
            var url = StringProp(asset, "browser_download_url");
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(url))
            {
                continue;
            }

            var size = asset.TryGetProperty("size", out var sizeEl) && sizeEl.TryGetInt64(out var bytes) ? bytes : 0L;
            assets.Add(new OrgReleaseAsset(name, size, url));
        }

        return assets;
    }

    private static string? FirstFailureAnnotation(string org, string repo, long runId)
    {
        var json = GhProcess.RunGh(
            ["api", $"repos/{org}/{repo}/actions/runs/{runId.ToString(CultureInfo.InvariantCulture)}/jobs?filter=latest&per_page=30"],
            ignoreFailure: true);
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("jobs", out var jobs) || jobs.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        string? chosen = null;
        foreach (var job in jobs.EnumerateArray())
        {
            var conclusion = StringProp(job, "conclusion");
            if (conclusion is not ("failure" or "cancelled"))
            {
                continue;
            }

            var checkUrl = StringProp(job, "check_run_url");
            var slash = checkUrl.LastIndexOf('/');
            if (slash < 0 || slash == checkUrl.Length - 1)
            {
                continue;
            }

            var checkId = checkUrl[(slash + 1)..];
            var annJson = GhProcess.RunGh(
                ["api", $"repos/{org}/{repo}/check-runs/{checkId}/annotations"],
                ignoreFailure: true);
            var message = FirstAnnotationMessage(annJson);
            if (string.IsNullOrWhiteSpace(message) || OrgFailureText.IsGenericProcessExit(message))
            {
                chosen ??= message;
                continue;
            }

            return message;
        }

        return chosen;
    }

    private static string? FirstAnnotationMessage(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            string? failure = null;
            string? warning = null;
            foreach (var ann in doc.RootElement.EnumerateArray())
            {
                var level = StringProp(ann, "annotation_level");
                var message = StringProp(ann, "message");
                if (string.IsNullOrWhiteSpace(message))
                {
                    continue;
                }

                if (level == "failure")
                {
                    failure = OrgFailureText.Prefer(failure, message);
                    continue;
                }

                if (level == "warning" && !OrgFailureText.IsActionRuntimeNotice(message))
                {
                    warning ??= message;
                }
            }

            return failure ?? warning;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string StringProp(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? ""
            : "";

    private static bool BoolProp(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.True;

    private sealed record OrgRepoSignals(OrgWorkflowFact? Merge, OrgWorkflowFact? ReleaseRun, OrgReleaseFact? LatestRelease);
}
