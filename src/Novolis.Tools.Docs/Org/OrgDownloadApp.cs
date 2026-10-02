namespace Novolis.Tools.Docs.Org;

/// <summary>One product row across Windows, Android, and Linux.</summary>
public sealed class OrgDownloadApp
{
    /// <summary>Spaced display name.</summary>
    public string Name { get; init; } = "";

    /// <summary>Windows installers.</summary>
    public List<OrgStatusAsset> Windows { get; } = [];

    /// <summary>Android packages.</summary>
    public List<OrgStatusAsset> Android { get; } = [];

    /// <summary>Linux archives.</summary>
    public List<OrgStatusAsset> Linux { get; } = [];

    /// <summary>Assets that did not match a known channel.</summary>
    public List<OrgStatusAsset> Other { get; } = [];
}
