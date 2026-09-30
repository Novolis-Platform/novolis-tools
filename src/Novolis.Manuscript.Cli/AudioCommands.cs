using Novolis.Manuscript;
using Novolis.Manuscript.Export.Audio;

namespace Novolis.Manuscript.Cli;

static class AudioCommands
{
    public static async Task<int> RunAsync(string[] args)
    {
        var opts = AudioCliOptions.Parse(args);
        if (opts.Help)
        {
            PrintHelp();
            return 0;
        }

        if (!ManuscriptWorkspace.TryOpen(opts.Workspace ?? Directory.GetCurrentDirectory(), out var ws) || ws is null)
            throw new InvalidOperationException("Not a manuscript workspace.");

        var book = ws.Catalog.FindBook(ws.ContentRoot, opts.Series, opts.Book!)
                   ?? throw new FileNotFoundException($"Book not found: {opts.Series}/{opts.Book}");

        var chapters = book.Chapters
            .Where(c => c.Kind == ChapterKind.Chapter)
            .Where(c => SelectChapter(c, opts))
            .Select(c => new AudiobookChapterInput(c.Id, c.Title, c.FilePath))
            .ToList();
        if (chapters.Count == 0)
            throw new InvalidOperationException("No chapters matched the selection.");

        var voice = string.IsNullOrWhiteSpace(opts.VoiceMap)
            ? new VoiceSettings()
            : VoiceMapStore.Load(opts.VoiceMap);

        if (opts.DryRun)
        {
            foreach (var chapter in chapters)
            {
                var markdown = File.ReadAllText(chapter.MarkdownPath);
                var plan = SpeechPlanner.Create(markdown, speakTitle: opts.SpeakTitle);
                Console.WriteLine($"{chapter.Id}: {plan.Segments.Count} segments");
            }

            return 0;
        }

        if (opts.VerifyOnly)
        {
            var outDir = ResolveOutDir(ws.ContentRoot, opts, book);
            AudiobookVerifier.VerifyOrThrow(outDir);
            Console.WriteLine($"Verified audiobook under {outDir}");
            return 0;
        }

        var outputDir = ResolveOutDir(ws.ContentRoot, opts, book);
        Directory.CreateDirectory(outputDir);
        var endpointText = opts.AzureEndpoint ??
            Environment.GetEnvironmentVariable("NOVOLIS_AZURE_SPEECH_ENDPOINT");
        var key = opts.AzureKey ??
            Environment.GetEnvironmentVariable("NOVOLIS_AZURE_SPEECH_KEY");
        if (!Uri.TryCreate(endpointText, UriKind.Absolute, out var endpoint) ||
            !string.Equals(endpoint.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(key))
        {
            throw new InvalidOperationException(
                "Azure Speech synthesis requires an HTTPS endpoint and key. " +
                "Use --azure-endpoint/--azure-key or NOVOLIS_AZURE_SPEECH_ENDPOINT/NOVOLIS_AZURE_SPEECH_KEY.");
        }

        var synthesizer = new AzureSpeechSynthesizer(endpoint, key);
        var pipeline = new AudiobookPipeline(synthesizer);
        var options = new AudiobookOptions
        {
            OutputDirectory = outputDir,
            AssembleMode = opts.Assemble,
            ParallelJobs = opts.Jobs,
            Force = opts.Force,
        };

        var progress = new Progress<AudiobookProgress>(p => Console.WriteLine(p.Message));
        var result = await pipeline.GenerateAsync(book.Id, chapters, voice, options, progress).ConfigureAwait(false);
        Console.WriteLine($"Manifest: {result.ManifestPath}");
        if (result.M4bPath is not null)
            Console.WriteLine($"M4B: {result.M4bPath}");
        if (result.ConcatenatedMp3Path is not null)
            Console.WriteLine($"MP3: {result.ConcatenatedMp3Path}");
        return 0;
    }

    static string ResolveOutDir(string root, AudioCliOptions opts, BookInfo book)
    {
        if (!string.IsNullOrWhiteSpace(opts.OutputDir))
            return Path.GetFullPath(opts.OutputDir);
        var series = book.SeriesId ?? opts.Series ?? "books";
        return string.Equals(series, "books", StringComparison.OrdinalIgnoreCase)
            ? Path.Combine(root, "out", book.Id, "audio")
            : Path.Combine(root, "out", series, book.Id, "audio");
    }

    static bool SelectChapter(ChapterInfo chapter, AudioCliOptions opts)
    {
        if (!string.IsNullOrWhiteSpace(opts.ChapterStem))
            return chapter.Id.Equals(opts.ChapterStem, StringComparison.OrdinalIgnoreCase);

        var key = ChapterOrder.GetFilenameSortKey(chapter.FilePath);
        if (opts.From is double from && key < from)
            return false;
        if (opts.To is double to && key > to)
            return false;
        return true;
    }

    static void PrintHelp()
    {
        Console.WriteLine("""
            novolis-manuscript audio --series ID --book ID [options]

              --voice-map PATH     Voice map YAML (optional)
              --azure-endpoint URI Azure Speech resource endpoint (or environment variable)
              --azure-key KEY      Azure Speech resource key (or environment variable)
              --from N --to N      Chapter order range
              --chapter STEM       Single chapter id/stem
              --jobs N             Parallel synthesis jobs (default 2)
              --force              Regenerate existing chapter audio
              --dry-run            Plan only
              --verify             Verify existing output directory
              --output DIR         Override output directory
              --assemble both|mp3|m4b|none
              --speak-title
              --workspace PATH
            """);
    }
}
