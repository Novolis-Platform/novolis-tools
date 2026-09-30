namespace Novolis.Tools.CodeLayout;

internal interface IGitFileTracker
{
    ValueTask<GitTrackingResult> TrackAsync(
        IReadOnlyCollection<string> filePaths,
        CancellationToken cancellationToken);
}
