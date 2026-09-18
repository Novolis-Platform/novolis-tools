using Novolis.Manuscript;
using Novolis.Manuscript.Metrics;

namespace Novolis.Manuscript.Cli;

static class MetricsCommands
{
    public static int Run(string[] args)
    {
        var opts = MetricsCliOptions.Parse(args);
        if (opts.Help)
        {
            Console.WriteLine("""
                novolis-manuscript metrics [options]

                  (default)               Metrics for all books
                  --series ID --book ID   One book
                  --workspace PATH
                """);
            return 0;
        }

        var root = opts.Workspace ?? Directory.GetCurrentDirectory();
        if (!ManuscriptWorkspace.TryOpen(root, out var ws) || ws is null)
            throw new InvalidOperationException("Not a manuscript workspace.");

        if (!string.IsNullOrWhiteSpace(opts.Book))
        {
            var dto = ManuscriptMetrics.RunOne(ws.ContentRoot, opts.Series ?? "books", opts.Book);
            Console.WriteLine($"Metrics: {dto.Series}/{dto.Book} words={dto.TotalWords} todos={dto.TotalTodos}");
        }
        else
        {
            var all = ManuscriptMetrics.RunAll(ws.ContentRoot);
            Console.WriteLine($"Metrics: {all.Count} book(s); overview under out/metrics/");
        }

        return 0;
    }
}

sealed class MetricsCliOptions
{
    public bool Help { get; init; }
    public string? Workspace { get; init; }
    public string? Series { get; init; }
    public string? Book { get; init; }

    public static MetricsCliOptions Parse(string[] args)
    {
        string? workspace = null, series = null, book = null;
        var help = false;
        for (var i = 0; i < args.Length; i++)
        {
            var a = args[i];
            string Need() => i + 1 < args.Length ? args[++i] : throw new InvalidOperationException($"Missing value for {a}");
            switch (a)
            {
                case "-h":
                case "--help":
                    help = true;
                    break;
                case "--workspace":
                    workspace = Need();
                    break;
                case "--series":
                    series = Need();
                    break;
                case "--book":
                    book = Need();
                    break;
                default:
                    throw new InvalidOperationException($"Unknown option: {a}");
            }
        }

        return new MetricsCliOptions { Help = help, Workspace = workspace, Series = series, Book = book };
    }
}
