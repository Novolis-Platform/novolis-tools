namespace Novolis.Tools.Docs.Graph;

/// <summary>Kind of relationship node in a project / package graph.</summary>
public enum GraphNodeKind
{
    /// <summary>Unknown or generic node.</summary>
    Unknown = 0,

    /// <summary>A local project (.csproj).</summary>
    Project,

    /// <summary>A NuGet package identity.</summary>
    Package,

    /// <summary>A logical folder / area grouping.</summary>
    Area,

    /// <summary>An external system or boundary.</summary>
    External,
}
