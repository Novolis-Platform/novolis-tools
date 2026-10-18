using Novolis.Visual.Compile;

namespace Novolis.AssetStudio.Authoring;

/// <summary>
/// Coordinates revisioned compilation and publishes only the result belonging
/// to the newest submitted request.
/// </summary>
public sealed class AssetStudioCompileCoordinator
{
    private readonly object gate = new();
    private long nextRequestId;
    private long latestRequestId;
    private AssetStudioCompileResult? latestResult;

    /// <summary>
    /// The latest result that completed for the newest submitted request.
    /// </summary>
    public AssetStudioCompileResult? LatestResult
    {
        get
        {
            lock (gate)
            {
                return latestResult;
            }
        }
    }

    /// <summary>
    /// Identifier of the newest submitted request, or zero before submission.
    /// </summary>
    public long LatestRequestId
    {
        get
        {
            lock (gate)
            {
                return latestRequestId;
            }
        }
    }

    /// <summary>
    /// Compiles with the repository's deterministic compiler.
    /// </summary>
    public ValueTask<AssetStudioCompileResult> CompileAsync(
        AssetStudioDocument document,
        VisualCompileOptions? options = null,
        CancellationToken cancellationToken = default) =>
        SubmitAsync(
            document,
            static (request, token) =>
            {
                token.ThrowIfCancellationRequested();
                return ValueTask.FromResult(
                    VisualCompiler.Compile(request.Document.Recipe, request.Options));
            },
            options,
            cancellationToken);

    /// <summary>
    /// Submits a caller-supplied compiler while preserving latest-result
    /// semantics. The delegate receives an immutable request snapshot.
    /// </summary>
    public async ValueTask<AssetStudioCompileResult> SubmitAsync(
        AssetStudioDocument document,
        Func<AssetStudioCompileRequest, CancellationToken, ValueTask<VisualIR>>
            compiler,
        VisualCompileOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(compiler);

        var requestId = Interlocked.Increment(ref nextRequestId);
        var request = new AssetStudioCompileRequest(
            requestId,
            document,
            options);
        lock (gate)
        {
            latestRequestId = requestId;
        }

        var compilation = await compiler(request, cancellationToken)
            .ConfigureAwait(false);
        ArgumentNullException.ThrowIfNull(compilation);

        var diagnostics = compilation.Diagnostics
            .Select(diagnostic =>
                AssetStudioDiagnostic.From(
                    diagnostic,
                    request.Document.Revision));
        var isLatest = false;
        AssetStudioCompileResult result;
        lock (gate)
        {
            isLatest = requestId == latestRequestId;
            result = new AssetStudioCompileResult(
                requestId,
                request.Document.Revision,
                request.Document.SemanticRevision,
                compilation,
                diagnostics,
                isLatest);
            if (isLatest)
            {
                latestResult = result;
            }
        }

        return result;
    }
}
