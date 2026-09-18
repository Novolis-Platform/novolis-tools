using Novolis.Manuscript;
using Novolis.Manuscript.Export.Pdf;

namespace Novolis.Manuscript.Cli;

static class PrintCommands
{
    public static int Run(string[] args)
    {
        var opts = PrintCliOptions.Parse(args);
        if (opts.Help)
        {
            PrintHelp();
            return 0;
        }

        if (!ManuscriptWorkspace.TryOpen(opts.Workspace ?? Directory.GetCurrentDirectory(), out var ws) || ws is null)
            throw new InvalidOperationException("Not a manuscript workspace.");

        if (opts.Reference)
        {
            var seriesList = string.IsNullOrWhiteSpace(opts.Series)
                ? ws.Catalog.Load(ws.ContentRoot).ToList()
                : [ws.Catalog.Load(ws.ContentRoot)
                    .FirstOrDefault(s => s.Id.Equals(opts.Series, StringComparison.OrdinalIgnoreCase))
                   ?? throw new FileNotFoundException($"Series not found: {opts.Series}")];

            foreach (var series in seriesList)
            {
                var refsDir = Path.Combine(series.DirectoryPath, "References");
                if (!Directory.Exists(refsDir))
                    refsDir = Path.Combine(series.DirectoryPath, "references");
                if (!Directory.Exists(refsDir))
                {
                    Console.WriteLine($"Skipping reference (no References/): {series.Id}");
                    continue;
                }

                var outDir = Path.Combine(ws.ContentRoot, "out", series.Id);
                var paths = ReferenceManualExporter.Export(refsDir, outDir, series.Id, series.Title);
                Console.WriteLine($"Reference PDF: {paths.PdfPath}");
            }

            // Standalone nonfiction books may have References/
            foreach (var book in ws.Catalog.LoadStandaloneBooks(ws.ContentRoot))
            {
                var refsDir = Path.Combine(book.DirectoryPath, "References");
                if (!Directory.Exists(refsDir))
                    continue;
                var outDir = Path.Combine(ws.ContentRoot, "out", book.Id);
                var paths = ReferenceManualExporter.Export(refsDir, outDir, book.Id, book.Title + " Reference");
                Console.WriteLine($"Reference PDF: {paths.PdfPath}");
            }

            return 0;
        }

        var books = new List<BookInfo>();
        if (!string.IsNullOrWhiteSpace(opts.Book))
        {
            books.Add(ws.Catalog.FindBook(ws.ContentRoot, opts.Series, opts.Book)
                      ?? throw new FileNotFoundException($"Book not found: {opts.Series}/{opts.Book}"));
        }
        else
        {
            books.AddRange(ws.Catalog.Load(ws.ContentRoot).SelectMany(s => s.Books));
            books.AddRange(ws.Catalog.LoadStandaloneBooks(ws.ContentRoot));
        }

        foreach (var book in books)
        {
            var seriesId = book.SeriesId ?? "books";
            var outDir = string.Equals(seriesId, "books", StringComparison.OrdinalIgnoreCase)
                ? Path.Combine(ws.ContentRoot, "out", book.Id)
                : Path.Combine(ws.ContentRoot, "out", seriesId, book.Id);
            var paths = BookPrintExporter.ExportBookFolder(
                book.DirectoryPath,
                outDir,
                seriesId,
                book.Id,
                new BookPrintOptions
                {
                    DebugMode = opts.Debug,
                    PrintSettingsPath = opts.PrintSettings,
                    SeriesTitle = opts.Series is null
                        ? null
                        : ws.Catalog.Load(ws.ContentRoot).FirstOrDefault(s => s.Id == seriesId)?.Title,
                });
            Console.WriteLine($"PDF: {paths.PdfPath}");

            var mdPaths = Novolis.Manuscript.Export.Markdown.ManuscriptMarkdownExporter.ExportBook(
                book,
                outDir,
                new Novolis.Manuscript.Export.Markdown.ManuscriptMarkdownExportOptions
                {
                    AuthorMode = opts.Debug,
                    SeriesTitle = opts.Series is null
                        ? null
                        : ws.Catalog.Load(ws.ContentRoot).FirstOrDefault(s => s.Id == seriesId)?.Title,
                });
            Console.WriteLine($"Markdown: {mdPaths.ReaderMarkdownPath}");
        }

        return 0;
    }

    static void PrintHelp()
    {
        Console.WriteLine("""
            novolis-manuscript print [options]

              (default)               Print all books
              --series ID --book ID   Print one book
              --reference --series ID Print series reference manual
              --print-settings PATH
              --debug
              --workspace PATH
            """);
    }
}

sealed class PrintCliOptions
{
    public bool Help { get; init; }
    public string? Workspace { get; init; }
    public string? Series { get; init; }
    public string? Book { get; init; }
    public string? PrintSettings { get; init; }
    public bool Reference { get; init; }
    public bool Debug { get; init; }

    public static PrintCliOptions Parse(string[] args)
    {
        string? workspace = null, series = null, book = null, printSettings = null;
        var reference = false;
        var debug = false;
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
                case "--print-settings":
                    printSettings = Need();
                    break;
                case "--reference":
                    reference = true;
                    break;
                case "--debug":
                    debug = true;
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
        };
    }
}
