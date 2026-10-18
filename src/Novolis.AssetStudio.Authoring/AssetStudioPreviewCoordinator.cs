using System.Collections.Concurrent;
using Novolis.Visual.Compile;

namespace Novolis.AssetStudio.Authoring;

/// <summary>
/// Coordinates injected preview targets with bounded caching and late-result
/// protection.
/// </summary>
public sealed class AssetStudioPreviewCoordinator
{
    private readonly IReadOnlyDictionary<string, IAssetStudioPreviewTarget> _targets;
    private readonly ConcurrentDictionary<string, AssetStudioPreviewResult> _cache =
        new(StringComparer.Ordinal);
    private readonly object _gate = new();
    private long _nextRequestId;
    private long _latestRequestId;
    private AssetStudioPreviewResult? _latestResult;

    public AssetStudioPreviewCoordinator(
        IEnumerable<IAssetStudioPreviewTarget> targets,
        int maximumCacheEntries = 32)
    {
        ArgumentNullException.ThrowIfNull(targets);
        if (maximumCacheEntries < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumCacheEntries));
        }

        var map = new Dictionary<string, IAssetStudioPreviewTarget>(
            StringComparer.Ordinal);
        foreach (var target in targets)
        {
            ArgumentNullException.ThrowIfNull(target);
            if (!map.TryAdd(target.TargetId, target))
            {
                throw new ArgumentException(
                    $"Preview target '{target.TargetId}' is duplicated.",
                    nameof(targets));
            }
        }

        _targets = map;
        MaximumCacheEntries = maximumCacheEntries;
    }

    /// <summary>Maximum number of semantic preview frames retained.</summary>
    public int MaximumCacheEntries { get; }

    /// <summary>Registered targets and their advertised execution status.</summary>
    public IReadOnlyList<AssetStudioPreviewTargetInfo> Targets =>
        _targets.Values
            .Select(target => target.Info)
            .OrderBy(info => info.TargetId, StringComparer.Ordinal)
            .ToArray();

    /// <summary>The latest result that was allowed to publish.</summary>
    public AssetStudioPreviewResult? LatestResult
    {
        get
        {
            lock (_gate)
            {
                return _latestResult;
            }
        }
    }

    /// <summary>Requests one target against one immutable document snapshot.</summary>
    public async ValueTask<AssetStudioPreviewResult> RequestAsync(
        AssetStudioPreviewRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        var requestId = Interlocked.Increment(ref _nextRequestId);
        lock (_gate)
        {
            _latestRequestId = requestId;
        }

        if (_cache.TryGetValue(request.CacheKey, out var cached))
        {
            return Publish(requestId, cached)
                ? cached
                : cached.MarkStale();
        }

        if (!_targets.TryGetValue(request.TargetId, out var target))
        {
            var unsupported = new AssetStudioPreviewResult(
                request.TargetId,
                request.Document.Revision,
                success: false,
                "application/octet-stream",
                diagnostics:
                [
                    new AssetStudioDiagnostic(
                        AssetStudioDiagnosticSeverity.Error,
                        "AST-PREVIEW-UNSUPPORTED",
                        $"Preview target '{request.TargetId}' is not registered.",
                        request.Document.Revision),
                ],
                semanticFingerprint: request.SemanticFingerprint,
                kind: AssetStudioPreviewTargetKind.Custom);
            Publish(requestId, unsupported);
            return unsupported;
        }

        if (target.Info.Status == VisualCapabilityStatus.Unsupported)
        {
            var unavailable = new AssetStudioPreviewResult(
                target.TargetId,
                request.Document.Revision,
                success: false,
                "application/octet-stream",
                diagnostics:
                [
                    new AssetStudioDiagnostic(
                        AssetStudioDiagnosticSeverity.Error,
                        "AST-PREVIEW-UNSUPPORTED",
                        target.Info.Message ??
                        $"Preview target '{target.TargetId}' is unsupported.",
                        request.Document.Revision),
                ],
                semanticFingerprint: request.SemanticFingerprint,
                kind: target.Kind);
            Publish(requestId, unavailable);
            return unavailable;
        }

        var result = await target.PreviewAsync(request, cancellationToken)
            .ConfigureAwait(false);
        if (result.DocumentRevision != request.Document.Revision)
        {
            result = result.MarkStale();
        }
        if (result.SemanticFingerprint is not null &&
            !string.Equals(
                result.SemanticFingerprint,
                request.SemanticFingerprint,
                StringComparison.Ordinal))
        {
            result = result.MarkStale();
        }

        var isLatest = Publish(requestId, result);
        if (isLatest && result.Success)
        {
            _cache[request.CacheKey] = result;
            TrimCache();
        }

        return isLatest ? result : result.MarkStale();
    }

    private bool Publish(long requestId, AssetStudioPreviewResult result)
    {
        lock (_gate)
        {
            if (requestId != _latestRequestId)
            {
                return false;
            }

            _latestResult = result;
            return true;
        }
    }

    private void TrimCache()
    {
        while (_cache.Count > MaximumCacheEntries &&
               _cache.Keys.Order(StringComparer.Ordinal).FirstOrDefault()
                   is { } oldest)
        {
            _cache.TryRemove(oldest, out _);
        }
    }
}
