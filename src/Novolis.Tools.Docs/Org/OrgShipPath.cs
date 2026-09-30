namespace Novolis.Tools.Docs.Org;

/// <summary>How a repository ships.</summary>
internal static class OrgShipPath
{
    /// <summary>
    /// Product hosts. <c>release.yml</c> publishes APKs and installers.
    /// Libraries publish packages from <c>merge.yml</c>; a GitHub Release tag is not that ship.
    /// </summary>
    public static bool PublishesInstallers(string repo) =>
        string.Equals(repo, "novolis-apps", StringComparison.OrdinalIgnoreCase);
}
