namespace Novolis.Tools.Docs.Org;

/// <summary>Per-repository facts before they are shaped into the snapshot.</summary>
internal sealed record OrgRepoFacts(
    string Name,
    int PackageCount,
    string GprVersion,
    string NugetVersion,
    OrgWorkflowFact? Merge,
    OrgWorkflowFact? ReleaseRun,
    OrgReleaseFact? LatestRelease);

