namespace Novolis.Tools.Coverage;

/// <summary>Evaluate aggregate line/branch against a threshold.</summary>
public static class CoverageGate
{
    /// <summary>
    /// Fail when line or branch percent is below <paramref name="failBelow"/>.
    /// Negative <paramref name="failBelow"/> disables the gate.
    /// </summary>
    public static (bool Failed, string? Message) Evaluate(CoberturaSummary summary, double failBelow)
    {
        if (failBelow <= 0)
            return (false, null);

        if (summary.LinePercent < failBelow)
        {
            return (true,
                $"Aggregate line coverage {summary.LinePercent.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)}% is below FailBelow={failBelow.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture)}%.");
        }

        if (summary.BranchesValid > 0 && summary.BranchPercent < failBelow)
        {
            return (true,
                $"Aggregate branch coverage {summary.BranchPercent.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)}% is below FailBelow={failBelow.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture)}%.");
        }

        return (false, null);
    }

    /// <summary>Evaluate a Cobertura file path.</summary>
    public static (bool Failed, string? Message) EvaluateFile(string coberturaPath, double failBelow) =>
        Evaluate(CoberturaSummaryParser.Parse(coberturaPath), failBelow);
}
