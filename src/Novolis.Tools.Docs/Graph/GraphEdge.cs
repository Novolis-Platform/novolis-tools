namespace Novolis.Tools.Docs.Graph;

/// <summary>A directed edge in a <see cref="RelationshipGraph"/>.</summary>
/// <param name="FromId">Source node id.</param>
/// <param name="ToId">Target node id.</param>
/// <param name="Label">Optional edge label (e.g. PackageReference).</param>
public sealed record GraphEdge(string FromId, string ToId, string? Label = null);
