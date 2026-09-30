using System.Text.Json;

namespace Novolis.Tools.Docs.Org;

/// <summary>JSON snapshot of org release failures, published releases, and package versions.</summary>
public sealed class OrgStatusSnapshot
{
    /// <summary>UTC timestamp of the collection run.</summary>
    public string GeneratedAt { get; set; } = "";

    /// <summary>GitHub organization login.</summary>
    public string Org { get; set; } = "";

    /// <summary>Public non-archived repositories.</summary>
    public int RepoCount { get; set; }

    /// <summary>Repositories whose latest merge.yml run succeeded.</summary>
    public int MergeSuccesses { get; set; }

    /// <summary>NuGet packages on GitHub Packages.</summary>
    public int PackageCount { get; set; }

    /// <summary>Library merge failures, plus app release failures. A library GitHub Release is not a ship.</summary>
    public int FailedCount { get; set; }

    /// <summary>App repositories whose latest GitHub Release published installers or other assets.</summary>
    public int ReleasedRepoCount { get; set; }

    /// <summary>Failed or cancelled runs, newest first.</summary>
    public List<OrgStatusFailure> Failures { get; set; } = [];

    /// <summary>Latest GitHub Release per repository, newest first.</summary>
    public List<OrgStatusRelease> Releases { get; set; } = [];

    /// <summary>One row per repository, alphabetical.</summary>
    public List<OrgStatusRepo> Repos { get; set; } = [];

    /// <summary>Reads a snapshot written by <c>novolis-docs org-readme</c>.</summary>
    public static OrgStatusSnapshot Load(string path)
    {
        var snapshot = JsonSerializer.Deserialize<OrgStatusSnapshot>(File.ReadAllText(path), OrgStatusJson.Options);
        if (snapshot is null)
        {
            throw new InvalidOperationException($"Empty status snapshot: {path}");
        }

        return snapshot;
    }
}
