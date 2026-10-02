namespace Novolis.Tools.Docs.Org;

/// <summary>One file attached to a GitHub Release.</summary>
internal sealed record OrgReleaseAsset(string Name, long Size, string DownloadUrl);
