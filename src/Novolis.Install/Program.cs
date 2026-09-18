using Novolis.Install.Commands;

namespace Novolis.Install;

/// <summary>
/// Entry point for the <c>novolis</c> global tool.
/// </summary>
public static class Program
{
    /// <summary>
    /// Runs the CLI with the supplied command-line arguments.
    /// </summary>
    /// <param name="args">Command-line arguments passed to the tool.</param>
    /// <returns>The process exit code from the parsed command.</returns>
    public static Task<int> Main(string[] args) => RunAsync(args);

    internal static Task<int> RunAsync(string[] args)
    {
        var root = CommandBuilder.Build();
        return root.Parse(args).InvokeAsync();
    }
}
