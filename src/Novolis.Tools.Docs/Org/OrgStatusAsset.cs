namespace Novolis.Tools.Docs.Org;

/// <summary>Downloadable file on a shipped GitHub Release.</summary>
public sealed class OrgStatusAsset
{
    /// <summary>Asset file name.</summary>
    public string Name { get; set; } = "";

    /// <summary>Size in bytes.</summary>
    public long Size { get; set; }

    /// <summary>Browser download URL.</summary>
    public string Url { get; set; } = "";
}
