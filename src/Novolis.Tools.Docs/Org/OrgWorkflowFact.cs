namespace Novolis.Tools.Docs.Org;

/// <summary>Latest completed workflow run used while composing a snapshot.</summary>
internal sealed record OrgWorkflowFact(
    string WorkflowFile,
    string Conclusion,
    string Title,
    string HtmlUrl,
    string CreatedAt,
    string? Error);

