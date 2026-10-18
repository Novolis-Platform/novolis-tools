using Novolis.Visual.Definitions;

namespace Novolis.AssetStudio.Authoring;

/// <summary>
/// A deterministic graph projection connection.
/// </summary>
public sealed record AssetStudioGraphConnection
{
    /// <summary>
    /// Creates a graph connection projection.
    /// </summary>
    public AssetStudioGraphConnection(
        ConnectionId id,
        VisualPortReference source,
        VisualPortReference target)
    {
        if (id.IsEmpty)
        {
            throw new ArgumentException(
                "A graph connection identifier is required.",
                nameof(id));
        }

        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);
        Id = id;
        Source = source;
        Target = target;
    }

    /// <summary>
    /// Connection identifier.
    /// </summary>
    public ConnectionId Id { get; }

    /// <summary>
    /// Source endpoint.
    /// </summary>
    public VisualPortReference Source { get; }

    /// <summary>
    /// Target endpoint.
    /// </summary>
    public VisualPortReference Target { get; }
}
