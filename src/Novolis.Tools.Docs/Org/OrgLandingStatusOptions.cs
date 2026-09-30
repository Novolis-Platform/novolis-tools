namespace Novolis.Tools.Docs.Org;

/// <summary>Options for regenerating org profile README status tables.</summary>
public sealed class OrgLandingStatusOptions
{
    /// <summary>GitHub organization login.</summary>
    public string Org { get; init; } = "Novolis-Platform";

    /// <summary>Path to profile/README.md.</summary>
    public required string ProfileReadmePath { get; init; }

    /// <summary>Max parallel gh/API calls.</summary>
    public int ThrottleLimit { get; init; } = 16;

    /// <summary>Max package IDs shown per repository row. Retained so existing callers keep parsing.</summary>
    public int MaxPackagesPerRepo { get; init; } = 3;

    /// <summary>Optional path for the status JSON consumed by the portfolio docs site.</summary>
    public string? StatusJsonPath { get; init; }
}
