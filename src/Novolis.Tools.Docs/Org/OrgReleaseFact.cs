namespace Novolis.Tools.Docs.Org;

/// <summary>Latest published GitHub Release used while composing a snapshot.</summary>
internal sealed record OrgReleaseFact(string Tag, string PublishedAt, string HtmlUrl, int AssetCount);
