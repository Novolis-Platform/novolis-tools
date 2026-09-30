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
