namespace Novolis.Tools.Doom.Cli;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        try
        {
            return await DoomCommandLine.CreateRootCommand().Parse(args).InvokeAsync().ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is InvalidDataException or IOException or ArgumentException)
        {
            Console.Error.WriteLine(exception.Message);
            return 1;
        }
    }
}
