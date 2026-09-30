using Novolis.Manuscript.Export.Pdf;

namespace Novolis.Manuscript.Cli;

sealed class PrintCliOptions
{
    public bool Help { get; init; }
    public string? Workspace { get; init; }
    public string? Series { get; init; }
    public string? Book { get; init; }
    public string? PrintSettings { get; init; }
    public bool Reference { get; init; }
    public bool Debug { get; init; }
    public BookPdfOutput PdfOutput { get; init; } = BookPdfOutput.Combine;

    public static PrintCliOptions Parse(string[] args)
    {
        string? workspace = null, series = null, book = null, printSettings = null;
        var reference = false;
        var debug = false;
        var help = false;
        var combine = false;
        var chapters = false;
        var both = false;
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
                case "--print-settings":
                    printSettings = Need();
                    break;
                case "--reference":
                    reference = true;
                    break;
                case "--debug":
                    debug = true;
                    break;
                case "--combine":
                    combine = true;
                    break;
                case "--chapters":
                    chapters = true;
                    break;
                case "--both":
                    both = true;
                    break;
                default:
                    throw new InvalidOperationException($"Unknown option: {a}");
            }
        }

        return new PrintCliOptions
        {
            Help = help,
            Workspace = workspace,
            Series = series,
            Book = book,
            PrintSettings = printSettings,
            Reference = reference,
            Debug = debug,
            PdfOutput = ResolvePdfOutput(combine, chapters, both),
        };
    }

    static BookPdfOutput ResolvePdfOutput(bool combine, bool chapters, bool both)
    {
        if (both || (combine && chapters))
            return BookPdfOutput.Both;
        if (chapters)
            return BookPdfOutput.Chapters;
        return BookPdfOutput.Combine;
    }
}
