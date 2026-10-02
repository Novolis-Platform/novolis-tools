using System.Globalization;

namespace Novolis.Tools.Docs.Org;

/// <summary>Shapes collected repository facts into the status snapshot.</summary>
internal static class OrgStatusSnapshotFactory
{
    /// <summary>Builds failures, shipped releases, and the inventory.</summary>
    public static OrgStatusSnapshot Create(
        string org,
        string generatedAt,
        int packageCount,
        IReadOnlyList<OrgRepoFacts> repos)
    {
        var failures = new List<OrgStatusFailure>();
        var releases = new List<OrgStatusRelease>();
        var rows = new List<OrgStatusRepo>();
        var mergeSuccesses = 0;

        foreach (var repo in repos)
        {
            if (repo.Merge?.Conclusion == "success")
            {
                mergeSuccesses++;
            }

            AddFailure(failures, repo.Name, repo.Merge);
            if (OrgShipPath.PublishesInstallers(repo.Name))
            {
                AddFailure(failures, repo.Name, repo.ReleaseRun);
            }

            var row = new OrgStatusRepo
            {
                Name = repo.Name,
                PackageCount = repo.PackageCount,
                GprVersion = repo.GprVersion,
                NugetVersion = repo.NugetVersion,
                MergeConclusion = repo.Merge?.Conclusion ?? "",
                MergeUrl = repo.Merge?.HtmlUrl ?? "",
                ReleaseConclusion = repo.ReleaseRun?.Conclusion ?? "",
                ReleaseRunUrl = repo.ReleaseRun?.HtmlUrl ?? "",
            };

            if (repo.LatestRelease is { } release)
            {
                row.ReleaseTag = release.Tag;
                row.ReleasePublished = FormatWhen(release.PublishedAt);
                row.ReleaseUrl = release.HtmlUrl;
                if (CountsAsShipped(repo, release))
                {
                    releases.Add(new OrgStatusRelease
                    {
                        Repo = repo.Name,
                        Tag = release.Tag,
                        Published = row.ReleasePublished,
                        Url = release.HtmlUrl,
                        Channel = Channel(release),
                        Assets = (release.Assets ?? [])
                            .Select(static asset => new OrgStatusAsset
                            {
                                Name = asset.Name,
                                Size = asset.Size,
                                Url = asset.DownloadUrl,
                            })
                            .ToList(),
                    });
                }
            }

            rows.Add(row);
        }

        failures.Sort(static (a, b) => string.Compare(b.When, a.When, StringComparison.Ordinal));
        releases.Sort(static (a, b) => string.Compare(b.Published, a.Published, StringComparison.Ordinal));
        rows.Sort(static (a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));

        return new OrgStatusSnapshot
        {
            GeneratedAt = generatedAt,
            Org = org,
            RepoCount = repos.Count,
            MergeSuccesses = mergeSuccesses,
            PackageCount = packageCount,
            FailedCount = failures.Count,
            ReleasedRepoCount = releases.Count,
            Failures = failures,
            Releases = releases,
            Repos = rows,
        };
    }

    private static void AddFailure(List<OrgStatusFailure> failures, string repo, OrgWorkflowFact? run)
    {
        if (run is null || run.Conclusion is not ("failure" or "cancelled"))
        {
            return;
        }

        if (OrgFailureText.IsSupersededCancellation(run.Error))
        {
            return;
        }

        var title = run.Title.Length <= 72 ? run.Title : run.Title[..72] + "…";
        var error = run.Error ?? "";
        if (error.Length > 180)
        {
            error = error[..180] + "…";
        }

        failures.Add(new OrgStatusFailure
        {
            Repo = repo,
            Workflow = run.WorkflowFile,
            Conclusion = run.Conclusion,
            When = FormatWhen(run.CreatedAt),
            Title = title,
            Url = run.HtmlUrl,
            Error = error.Replace('\r', ' ').Replace('\n', ' ').Trim(),
        });
    }

    /// <summary>A library tag with no uploaded APKs or installers did not ship.</summary>
    private static bool CountsAsShipped(OrgRepoFacts repo, OrgReleaseFact release) =>
        OrgShipPath.PublishesInstallers(repo.Name) && CountAssets(release) > 0;

    private static string Channel(OrgReleaseFact release)
    {
        var count = CountAssets(release);
        return count == 1 ? "1 release asset" : $"{count} release assets";
    }

    private static int CountAssets(OrgReleaseFact release) =>
        release.Assets is { Count: > 0 } ? release.Assets.Count : release.AssetCount;

    /// <summary>Formats a GitHub timestamp as UTC. Unparseable values pass through.</summary>
    public static string FormatWhen(string? iso)
    {
        if (string.IsNullOrWhiteSpace(iso))
        {
            return "";
        }

        if (DateTimeOffset.TryParse(iso, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dto))
        {
            return dto.UtcDateTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) + " UTC";
        }

        return iso;
    }
}
