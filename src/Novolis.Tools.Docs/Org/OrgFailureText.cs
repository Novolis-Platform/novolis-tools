namespace Novolis.Tools.Docs.Org;

/// <summary>Decides which workflow conclusions belong on the Failed list.</summary>
internal static class OrgFailureText
{
    /// <summary>GitHub cancels the older run when a newer one is queued. That is not a result.</summary>
    public static bool IsSupersededCancellation(string? error) =>
        !string.IsNullOrWhiteSpace(error) &&
        error.Contains("higher priority waiting request", StringComparison.OrdinalIgnoreCase);

    /// <summary>The runner's own exit line, not the step that failed.</summary>
    public static bool IsGenericProcessExit(string? error) =>
        !string.IsNullOrWhiteSpace(error) &&
        error.Contains("Process completed with exit code", StringComparison.OrdinalIgnoreCase);

    /// <summary>Keeps a specific annotation ahead of the runner's exit-code line.</summary>
    public static string? Prefer(string? current, string? candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return current;
        }

        if (string.IsNullOrWhiteSpace(current) ||
            (IsGenericProcessExit(current) && !IsGenericProcessExit(candidate)))
        {
            return candidate;
        }

        return current;
    }
}
