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
            var seriesTitle = opts.Series is null
                ? null
                : ws.Catalog.Load(ws.ContentRoot).FirstOrDefault(s => s.Id == seriesId)?.Title;
            var paths = BookPrintExporter.ExportBookFolder(
                book.DirectoryPath,
                outDir,
                seriesId,
                book.Id,
                new BookPrintOptions
                {
                    DebugMode = opts.Debug,
                    PrintSettingsPath = opts.PrintSettings,
                    SeriesTitle = seriesTitle,
                    PdfOutput = opts.PdfOutput,
                });
            if (opts.PdfOutput is BookPdfOutput.Combine or BookPdfOutput.Both)
                Console.WriteLine($"PDF: {paths.PdfPath}");
            if (opts.PdfOutput is BookPdfOutput.Chapters or BookPdfOutput.Both)
            {
                var chaptersDir = Path.Combine(outDir, book.Id + "-chapters");
                Console.WriteLine($"Chapter PDFs: {chaptersDir}");
            }

            if (opts.PdfOutput is BookPdfOutput.Combine or BookPdfOutput.Both)
            {
                var mdPaths = Novolis.Manuscript.Export.Markdown.ManuscriptMarkdownExporter.ExportBook(
                    book,
                    outDir,
                    new Novolis.Manuscript.Export.Markdown.ManuscriptMarkdownExportOptions
                    {
                        AuthorMode = opts.Debug,
                        SeriesTitle = seriesTitle,
                    });
                Console.WriteLine($"Markdown: {mdPaths.ReaderMarkdownPath}");
            }
        }

        return 0;
    }

    static void PrintHelp()
    {
        Console.WriteLine("""
            novolis-manuscript print [options]

              (default) / --combine   One combined PDF per book
              --chapters              Chapter PDFs only ({bookId}-chapters/)
              --both                  Combined PDF and chapter folder
              --combine --chapters    Same as --both
              --series ID --book ID   Print one book
              --reference --series ID Print series reference manual
              --print-settings PATH
              --debug
              --workspace PATH
            """);
    }
}
