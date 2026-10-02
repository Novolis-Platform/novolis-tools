namespace Novolis.Tools.Docs.Org;

/// <summary>Downloads for one shipped release.</summary>
public sealed class OrgDownloadRelease
{
    /// <summary>Repository name.</summary>
    public string Repo { get; init; } = "";

    /// <summary>Release tag.</summary>
    public string Tag { get; init; } = "";

    /// <summary>Release HTML URL.</summary>
    public string Url { get; init; } = "";

    /// <summary>Publish time, formatted UTC.</summary>
    public string Published { get; init; } = "";

    /// <summary>Apps in this release, alphabetical.</summary>
    public List<OrgDownloadApp> Apps { get; init; } = [];

    /// <summary>Checksum files kept beside the app table.</summary>
    public List<OrgStatusAsset> Checksums { get; init; } = [];
}
