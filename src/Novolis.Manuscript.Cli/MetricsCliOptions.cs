using Novolis.Manuscript;
using Novolis.Manuscript.Metrics;

namespace Novolis.Manuscript.Cli;

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
