namespace Novolis.Tools.Coverage;

/// <summary>
/// Throw-style gate for unit tests and scripts. Prefer <see cref="CoverageGate.Evaluate"/> when you need a bool.
/// </summary>
public static class CoverageAssert
{
    /// <summary>
    /// Throws <see cref="InvalidOperationException"/> when aggregate line or branch is below
    /// <paramref name="failBelow"/>.
    /// </summary>
    public static void AtLeast(string coberturaPath, double failBelow = 95)
    {
        var (failed, message) = CoverageGate.EvaluateFile(coberturaPath, failBelow);
        if (failed)
            throw new InvalidOperationException(message ?? "Coverage gate failed.");
    }

    /// <summary>Throws when <paramref name="summary"/> is below the gate.</summary>
    public static void AtLeast(CoberturaSummary summary, double failBelow = 95)
    {
        var (failed, message) = CoverageGate.Evaluate(summary, failBelow);
        if (failed)
            throw new InvalidOperationException(message ?? "Coverage gate failed.");
    }
}
