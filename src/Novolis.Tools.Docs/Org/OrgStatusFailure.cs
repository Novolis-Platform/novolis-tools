namespace Novolis.Tools.Docs.Org;

/// <summary>A merge or release workflow run that failed or was cancelled.</summary>
public sealed class OrgStatusFailure
{
    /// <summary>Repository name.</summary>
    public string Repo { get; set; } = "";

    /// <summary>Workflow file name.</summary>
    public string Workflow { get; set; } = "";

    /// <summary>GitHub Actions conclusion.</summary>
    public string Conclusion { get; set; } = "";

    /// <summary>Run creation time, formatted UTC.</summary>
    public string When { get; set; } = "";

    /// <summary>Run title.</summary>
    public string Title { get; set; } = "";

    /// <summary>Actions run URL.</summary>
    public string Url { get; set; } = "";

    /// <summary>First failing check annotation, when GitHub returned one.</summary>
    public string Error { get; set; } = "";
}
