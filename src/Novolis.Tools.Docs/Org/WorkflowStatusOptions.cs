namespace Novolis.Tools.Docs.Org;

/// <summary>Arguments for a GitHub Actions failure summary.</summary>
public sealed class WorkflowStatusOptions
{
    /// <summary>owner/name repository.</summary>
    public string Repo { get; init; } = "Novolis-Platform/novolis-apps";

    /// <summary>How many recent runs to list.</summary>
    public int Limit { get; init; } = 8;
}
