using Novolis.Visual.Compile;

namespace Novolis.AssetStudio.Authoring;

/// <summary>
/// One revisioned compile request submitted to the headless coordinator.
/// </summary>
public sealed record AssetStudioCompileRequest
{
    /// <summary>
    /// Creates a compile request.
    /// </summary>
    public AssetStudioCompileRequest(
        long requestId,
        AssetStudioDocument document,
        VisualCompileOptions? options = null)
    {
        if (requestId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(requestId));
        }

        ArgumentNullException.ThrowIfNull(document);
        RequestId = requestId;
        Document = document;
        Options = options ?? VisualCompileOptions.Default;
    }

    /// <summary>
    /// Monotonic coordinator request identifier.
    /// </summary>
    public long RequestId { get; }

    /// <summary>
    /// Document snapshot being compiled.
    /// </summary>
    public AssetStudioDocument Document { get; }

    /// <summary>
    /// Compiler options.
    /// </summary>
    public VisualCompileOptions Options { get; }

    /// <summary>
    /// Semantic revision captured by this request.
    /// </summary>
    public long SemanticRevision => Document.SemanticRevision;
}
