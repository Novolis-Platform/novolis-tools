using System.CommandLine;

namespace Novolis.AssetStudio.Cli;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        var root = AssetStudioCommandLine.CreateRootCommand();
        return await root.Parse(args).InvokeAsync().ConfigureAwait(false);
    }
}
