namespace Novolis.Tools.Docs.Org;

/// <summary>Inventory row for one repository.</summary>
public sealed class OrgStatusRepo
{
    /// <summary>Repository name.</summary>
    public string Name { get; set; } = "";

    /// <summary>NuGet packages linked to the repository.</summary>
    public int PackageCount { get; set; }

    /// <summary>Highest GitHub Packages version among those packages.</summary>
    public string GprVersion { get; set; } = "";

    /// <summary>Highest nuget.org version among those packages.</summary>
    public string NugetVersion { get; set; } = "";

    /// <summary>Latest release tag.</summary>
    public string ReleaseTag { get; set; } = "";

    /// <summary>Latest release publish time.</summary>
    public string ReleasePublished { get; set; } = "";

    /// <summary>Latest release URL.</summary>
    public string ReleaseUrl { get; set; } = "";

    /// <summary>Latest merge.yml conclusion.</summary>
    public string MergeConclusion { get; set; } = "";

    /// <summary>Latest merge.yml run URL.</summary>
    public string MergeUrl { get; set; } = "";

    /// <summary>Latest release.yml conclusion.</summary>
    public string ReleaseConclusion { get; set; } = "";

    /// <summary>Latest release.yml run URL.</summary>
    public string ReleaseRunUrl { get; set; } = "";
}
