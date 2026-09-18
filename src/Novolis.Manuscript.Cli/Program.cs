using Novolis.Manuscript.Export.Audio;

namespace Novolis.Manuscript.Cli;

static class Program
{
    static async Task<int> Main(string[] args)
    {
        if (args.Length == 0 || IsHelp(args[0]))
        {
            PrintRootHelp();
            return 0;
        }

        var verb = args[0].ToLowerInvariant();
        var rest = args.Skip(1).ToArray();
        try
        {
            return verb switch
            {
                "book" => BookCommands.Run(rest),
                "audio" => await AudioCommands.RunAsync(rest).ConfigureAwait(false),
                "print" => PrintCommands.Run(rest),
                "metrics" => MetricsCommands.Run(rest),
                _ => Fail($"Unknown verb '{args[0]}'. Use book|audio|print|metrics."),
            };
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"ERROR: {ex.Message}");
            return 1;
        }
    }

    static bool IsHelp(string value) =>
        value is "-h" or "--help" or "help" or "/?";

    static int Fail(string message)
    {
        Console.Error.WriteLine(message);
        PrintRootHelp();
        return 1;
    }

    static void PrintRootHelp()
    {
        Console.WriteLine("""
            novolis-manuscript — manuscript authoring / publish CLI

              book <command>     Chapter surgery, doctor, list-books, validate-*
              audio [options]    Audiobook generation via Export.Audio
              print [options]    Book/reference PDF+MD+HTML+TXT (Export.Pdf)
              metrics [options]  Word-count / TODO metrics

            Use '<verb> --help' for details.
            """);
    }
}
