using System.Diagnostics;
using System.Text;

namespace Novolis.Tools.Docs.Org;

/// <summary>Runs gh with redirected I/O and no console window.</summary>
public static class GhProcess
{
    /// <summary>Runs gh with the given argument list.</summary>
    public static string Run(string fileName, IReadOnlyList<string> arguments, bool ignoreFailure = false)
    {
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var arg in arguments)
        {
            psi.ArgumentList.Add(arg);
        }

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException($"{fileName} did not start.");

        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();

        if (process.ExitCode != 0 && !ignoreFailure)
        {
            throw new InvalidOperationException(
                $"{fileName} {FormatArgs(arguments)} failed ({process.ExitCode}): {stderr}{stdout}".Trim());
        }

        return stdout;
    }

    /// <summary>Runs gh and returns stdout (throws when exit code is non-zero).</summary>
    public static string RunGh(IReadOnlyList<string> arguments, bool ignoreFailure = false) =>
        Run("gh", arguments, ignoreFailure);

    private static string FormatArgs(IReadOnlyList<string> arguments)
    {
        var sb = new StringBuilder();
        foreach (var arg in arguments)
        {
            sb.Append(' ');
            sb.Append(arg.Contains(' ', StringComparison.Ordinal) ? $"\"{arg}\"" : arg);
        }

        return sb.ToString();
    }
}
