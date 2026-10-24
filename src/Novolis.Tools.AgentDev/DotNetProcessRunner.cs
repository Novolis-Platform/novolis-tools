using System.Diagnostics;

namespace Novolis.Tools.AgentDev;

public sealed class DotNetProcessRunner : IDevelopmentProcessRunner
{
    public DevelopmentProcessResult Run(string verb, string workingDirectory, CancellationToken cancellationToken)
    {
        var args = verb switch
        {
            "build" => "build --nologo",
            "test" => "test --nologo",
            _ => throw new InvalidOperationException($"Unsupported process verb '{verb}'."),
        };

        var start = new ProcessStartInfo("dotnet", args)
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var process = Process.Start(start) ?? throw new InvalidOperationException("dotnet did not start.");
        var stdout = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderr = process.StandardError.ReadToEndAsync(cancellationToken);
        process.WaitForExit();
        cancellationToken.ThrowIfCancellationRequested();
        return new DevelopmentProcessResult
        {
            ExitCode = process.ExitCode,
            StandardOutput = stdout.GetAwaiter().GetResult(),
            StandardError = stderr.GetAwaiter().GetResult(),
        };
    }
}
