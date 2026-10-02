namespace Novolis.Tools.Docs.Org;

/// <summary>Latest published GitHub Release for one repository.</summary>
public sealed class OrgStatusRelease
{
    /// <summary>Repository name.</summary>
    public string Repo { get; set; } = "";

    /// <summary>Release tag.</summary>
    public string Tag { get; set; } = "";

    /// <summary>Publish time, formatted UTC.</summary>
    public string Published { get; set; } = "";

    /// <summary>Release HTML URL.</summary>
    public string Url { get; set; } = "";

    /// <summary>nuget.org version, or release-asset count.</summary>
    public string Channel { get; set; } = "";

    /// <summary>Installer, package, and checksum files published with this release.</summary>
    public List<OrgStatusAsset> Assets { get; set; } = [];
}
